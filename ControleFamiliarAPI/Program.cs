using System.Text;
using ControleFamiliarAPI.Middlewares;
using ControleFamiliarAPI.Services.Implementations;
using ControleFamiliarAPI.Services.Interfaces;
using ControleGastos.Api.Data;
using ControleGastos.Api.Models;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
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

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(connectionString));

builder.Services
    .AddIdentityCore<Usuario>(options =>
    {
        options.Password.RequiredLength = 6;
        options.Password.RequireDigit = false;
        options.Password.RequireNonAlphanumeric = false;
        options.Password.RequireUppercase = false;
        options.Password.RequireLowercase = false;
        options.User.RequireUniqueEmail = true;
    })
    .AddRoles<IdentityRole<int>>()
    .AddEntityFrameworkStores<AppDbContext>();

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
    });

builder.Services.AddAuthorization();
builder.Services.AddHttpContextAccessor();

// Add services to the container.
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IFamiliaService, FamiliaService>();
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<IPessoaService, PessoaService>();
builder.Services.AddScoped<ICategoriaService, CategoriaService>();
builder.Services.AddScoped<ITransacaoService, TransacaoService>();
builder.Services.AddScoped<IRelatorioService, RelatorioService>();

var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
    ?? new[] { "http://localhost:5173" };

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowReact",
        policy =>
        {
            policy.WithOrigins(allowedOrigins)
                  .AllowAnyMethod()
                  .AllowAnyHeader();
        });
});

builder.Services.AddOpenApi(options =>
{
    var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);

    options.AddDocumentTransformer((document, context, cancellationToken) =>
    {
        document.Info.Description = "API Controle Familiar com documenta��o detalhada";
        return Task.CompletedTask;
    });
});

builder.Services.AddControllers();

// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();



var app = builder.Build();

// Aplica as migrations pendentes quando a aplicação sobe. O MSSQL free do
// MonsterASP.NET só aceita conexão de dentro do próprio datacenter deles
// (não de um runner externo do CI), então rodar "dotnet ef database update"
// no pipeline não é viável aqui — a própria aplicação, já rodando no
// datacenter, é quem aplica as migrations no primeiro start.
using (var scope = app.Services.CreateScope())
{
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
            autorizado = partes.Length == 2 && partes[0] == scalarUser && partes[1] == scalarPassword;
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

app.UseMiddleware<ErrorMiddleware>();
app.UseCors("AllowReact");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
