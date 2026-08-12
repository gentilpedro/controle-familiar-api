using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;
using System.Text;
using System.Threading.RateLimiting;
using ControleFamiliarAPI.Middlewares;
using ControleFamiliarAPI.Services;
using ControleFamiliarAPI.Services.Implementations;
using ControleFamiliarAPI.Services.Interfaces;
using ControleFamiliarAPI.Data;
using ControleFamiliarAPI.Models;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using ControleFamiliarAPI.OpenApi;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
if (string.IsNullOrWhiteSpace(connectionString))
    throw new InvalidOperationException(
        "ConnectionStrings:DefaultConnection não configurada. Em desenvolvimento, use 'dotnet user-secrets set ConnectionStrings:DefaultConnection \"...\"'; em produção, defina a variável de ambiente ConnectionStrings__DefaultConnection.");

var jwtKey = builder.Configuration["Jwt:Key"];
if (string.IsNullOrWhiteSpace(jwtKey))
    throw new InvalidOperationException(
        "Jwt:Key não configurada. Em desenvolvimento, use 'dotnet user-secrets set Jwt:Key \"...\"'; em produção, defina a variável de ambiente Jwt__Key.");

// Em testes de integração (ambiente "Testing"), o AppDbContext real (SQL
// Server) não é registrado aqui — o CustomWebApplicationFactory dos testes
// registra o próprio, apontando pra SQLite em memória.
if (!builder.Environment.IsEnvironment("Testing"))
{
    builder.Services.AddDbContext<AppDbContext>(options =>
        options.UseSqlServer(connectionString));
}

builder.Services
    .AddIdentityCore<Usuario>(options =>
    {
        options.Password.RequiredLength = 8;
        options.Password.RequireDigit = true;
        options.Password.RequireNonAlphanumeric = false;
        options.Password.RequireUppercase = false;
        options.Password.RequireLowercase = false;
        options.User.RequireUniqueEmail = true;

        // Bloqueia a conta temporariamente após tentativas de login seguidas
        // com senha errada — sem isso, nada impede brute force contra
        // /api/auth/login além do rate limiter por IP configurado abaixo.
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
        options.Lockout.AllowedForNewUsers = true;
    })
    .AddRoles<IdentityRole<int>>()
    .AddEntityFrameworkStores<AppDbContext>()
    .AddSignInManager()
    // Sem isso, GenerateEmailConfirmationTokenAsync/ConfirmEmailAsync
    // lançam "No IUserTwoFactorTokenProvider<TUser> named 'Default' is
    // registered" — o provider "Default" (DataProtectorTokenProvider) só
    // existe se for registrado explicitamente.
    .AddDefaultTokenProviders();

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        var jwtConfig = builder.Configuration.GetSection("Jwt");

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtConfig["Issuer"],
            ValidAudience = jwtConfig["Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtConfig["Key"]!))
        };

        // Assinatura/validade válidas não bastam: um token revogado via
        // logout (ver AuthService.Logout / TokensRevogados) precisa ser
        // rejeitado mesmo antes de vencer naturalmente.
        options.Events = new JwtBearerEvents
        {
            // O navegador manda a sessão no cookie HttpOnly, não no header
            // Authorization — o JavaScript nem enxerga o token para montar o
            // header. Este evento roda antes da validação e alimenta o handler
            // a partir do cookie.
            //
            // Authorization tem precedência sobre o cookie, e a checagem
            // precisa ser no header em si: este evento roda ANTES de o handler
            // ler o Authorization, então context.Token está sempre vazio aqui e
            // testá-lo não distinguiria nada — o cookie acabaria sobrescrevendo
            // o Bearer. Um cliente pode legitimamente ter os dois (ex.: usar
            // Bearer numa sessão de navegador que já tem cookie), e nesse caso
            // vale o que ele mandou explicitamente.
            OnMessageReceived = context =>
            {
                if (!context.Request.Headers.ContainsKey("Authorization")
                    && context.Request.Cookies.TryGetValue(CookieDeSessao.Nome, out var tokenDoCookie))
                {
                    context.Token = tokenDoCookie;
                }

                return Task.CompletedTask;
            },

            OnTokenValidated = async context =>
            {
                var jti = context.Principal?.FindFirst(JwtRegisteredClaimNames.Jti)?.Value;
                if (string.IsNullOrEmpty(jti))
                    return;

                var db = context.HttpContext.RequestServices.GetRequiredService<AppDbContext>();
                var revogado = await db.TokensRevogados.AnyAsync(t => t.Jti == jti);

                if (revogado)
                    context.Fail("Token revogado.");
            }
        };
    });

builder.Services.AddAuthorization();
builder.Services.AddHttpContextAccessor();

builder.Services.AddHealthChecks()
    .AddDbContextCheck<AppDbContext>();

// Cache curto usado pelo RelatorioService (ver comentário lá) — evita
// recalcular os totais da família do zero em chamadas consecutivas.
builder.Services.AddMemoryCache();

builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
});

builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();
builder.Services.AddScoped<IAuditoriaService, AuditoriaService>();
builder.Services.AddScoped<IFamiliaDtoFactory, FamiliaDtoFactory>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IFamiliaService, FamiliaService>();
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<IPessoaService, PessoaService>();
builder.Services.AddScoped<ICategoriaService, CategoriaService>();
builder.Services.AddScoped<ITransacaoService, TransacaoService>();
builder.Services.AddScoped<IRelatorioService, RelatorioService>();
builder.Services.AddScoped<IPainelMensalService, PainelMensalService>();

var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
    ?? new[] { "http://localhost:5173" };

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowReact",
        policy =>
        {
            policy.WithOrigins(allowedOrigins)
                  .AllowAnyMethod()
                  .AllowAnyHeader()
                  // Necessário para o navegador enviar o cookie de sessão numa
                  // chamada cross-origin. Em produção o proxy da Vercel faz
                  // tudo virar same-origin e o CORS nem entra em jogo; isto
                  // cobre quem rode o frontend apontando direto para a API.
                  // Só é válido com WithOrigins explícito — AllowAnyOrigin e
                  // AllowCredentials juntos são proibidos pela spec.
                  .AllowCredentials();
        });
});

// Limita tentativas de login/registro por IP — sem isso, nada impede
// brute force ou cadastro em massa contra /api/auth/*.
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.AddPolicy("auth", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "desconhecido",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));
});

builder.Services.AddOpenApi(options =>
{
    options.AddDocumentTransformer((document, context, cancellationToken) =>
    {
        document.Info.Description = "API Controle Familiar com documentação detalhada";
        return Task.CompletedTask;
    });

    // Publica no schema o que está escrito nos /// <summary> do código.
    options.AddSchemaTransformer<ComentariosXmlSchemaTransformer>();
});

builder.Services.AddControllers();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
    app.UseHsts();

app.UseHttpsRedirection();
app.UseResponseCompression();

// Aplica as migrations pendentes quando a aplicação sobe. O MSSQL free do
// MonsterASP.NET só aceita conexão de dentro do próprio datacenter deles
// (não de um runner externo do CI), então rodar "dotnet ef database update"
// no pipeline não é viável aqui — a própria aplicação, já rodando no
// datacenter, é quem aplica as migrations no primeiro start.
// Pulado no ambiente "Testing": lá o schema é criado direto do modelo
// (Database.EnsureCreated, ver ControleFamiliarAPI.Tests) contra SQLite, e
// as migrations aqui foram escritas especificamente para SQL Server.
if (!app.Environment.IsEnvironment("Testing"))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
}

// Documentação (Scalar): livre em desenvolvimento; em produção exige Basic
// Auth (usuário/senha vêm de Scalar:Username / Scalar:Password).
app.Use(async (context, next) =>
{
    var isDocsPath = context.Request.Path.StartsWithSegments("/scalar")
        || context.Request.Path.StartsWithSegments("/openapi");

    if (isDocsPath && !app.Environment.IsDevelopment())
    {
        var scalarUser = app.Configuration["Scalar:Username"];
        var scalarPassword = app.Configuration["Scalar:Password"];
        var authHeader = context.Request.Headers.Authorization.ToString();

        var autorizado = false;
        if (!string.IsNullOrEmpty(scalarUser) && authHeader.StartsWith("Basic ", StringComparison.Ordinal))
        {
            var credenciais = Encoding.UTF8.GetString(Convert.FromBase64String(authHeader["Basic ".Length..]));
            var partes = credenciais.Split(':', 2);

            // FixedTimeEquals em vez de == para não vazar, via timing, quantos
            // caracteres do usuário/senha estão corretos a cada tentativa.
            autorizado = partes.Length == 2
                && CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(partes[0]), Encoding.UTF8.GetBytes(scalarUser))
                && CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(partes[1]), Encoding.UTF8.GetBytes(scalarPassword ?? string.Empty));
        }

        if (!autorizado)
        {
            context.Response.Headers.WWWAuthenticate = "Basic realm=\"API Docs\"";
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }
    }

    await next();
});

app.MapOpenApi();
app.MapScalarApiReference();

// Sem autenticação de propósito: precisa ser alcançável por monitoramento
// externo (ou o próprio painel do MonsterASP.NET) sem token nenhum.
app.MapHealthChecks("/health");

app.UseMiddleware<ErrorMiddleware>();
app.UseCors("AllowReact");

// Depois do CORS (para o preflight ser respondido normalmente) e antes da
// autenticação: a checagem não depende de quem é o usuário, só de como a
// requisição chegou.
app.UseMiddleware<CsrfMiddleware>();

app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();

// Necessário para o WebApplicationFactory<Program> dos testes de integração
// enxergar essa classe — com top-level statements ela é gerada como
// "internal" por padrão.
public partial class Program
{
}
