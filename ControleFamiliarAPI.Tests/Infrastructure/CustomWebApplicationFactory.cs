using ControleFamiliarAPI.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ControleFamiliarAPI.Tests.Infrastructure;

/// <summary>
/// Sobe a API inteira em memória (pipeline HTTP real, DI real), trocando só
/// o AppDbContext (SQL Server em produção) por SQLite em memória — mantém
/// uma única conexão aberta durante a vida da factory, porque um banco
/// SQLite ":memory:" desaparece assim que a última conexão fecha.
/// </summary>
public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    public const string UsuarioDocs = "docs-teste";
    public const string SenhaDocs = "senha-docs-teste";

    private readonly SqliteConnection _connection = new("DataSource=:memory:");

    public CustomWebApplicationFactory()
    {
        // Precisa ser variável de ambiente, não builder.UseEnvironment()/
        // ConfigureAppConfiguration: Program.cs lê ambiente e config logo nas
        // primeiras linhas, antes de qualquer hook do WebApplicationFactory
        // rodar — env var é a única fonte que WebApplication.CreateBuilder já
        // enxerga desde o início. Com ASPNETCORE_ENVIRONMENT=Testing,
        // Program.cs pula o AddDbContext<AppDbContext> real (SQL Server) e o
        // Database.Migrate() automático — o AppDbContext de teste (SQLite em
        // memória) é registrado abaixo, em ConfigureWebHost.
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Testing");
        Environment.SetEnvironmentVariable("ConnectionStrings__DefaultConnection", "usado-apenas-para-satisfazer-a-checagem-de-startup");
        Environment.SetEnvironmentVariable("Jwt__Key", "chave-de-teste-somente-para-os-testes-automatizados-0123456789");
        Environment.SetEnvironmentVariable("Jwt__Issuer", "ControleFamiliarAPI.Tests");
        Environment.SetEnvironmentVariable("Jwt__Audience", "ControleFamiliarWeb.Tests");
        Environment.SetEnvironmentVariable("Jwt__ExpiraHoras", "12");

        // Fora de Development, /openapi e /scalar exigem Basic Auth. Sem estes
        // valores o documento OpenAPI responderia 401 e não daria para testá-lo.
        Environment.SetEnvironmentVariable("Scalar__Username", UsuarioDocs);
        Environment.SetEnvironmentVariable("Scalar__Password", SenhaDocs);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            _connection.Open();

            services.AddDbContext<AppDbContext>(options => options.UseSqlite(_connection));
        });
    }

    /// <summary>
    /// Cria o schema no SQLite em memória a partir do modelo atual do EF
    /// Core. Precisa ser chamado antes de qualquer requisição do teste.
    /// </summary>
    public async Task InicializarBancoAsync()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.EnsureCreatedAsync();

        // Em produção o catálogo do sistema entra pela migration
        // SeedCategoriasDoSistema. Aqui o schema vem de EnsureCreated a partir
        // do modelo, sem passar por migration nenhuma — então ele precisa ser
        // inserido na mão, ou os testes rodariam contra um catálogo vazio.
        db.Categorias.AddRange(CategoriasPadrao.DoSistema());
        await db.SaveChangesAsync();
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing)
            _connection.Dispose();
    }
}
