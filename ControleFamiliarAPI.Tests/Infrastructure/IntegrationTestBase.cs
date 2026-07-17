namespace ControleFamiliarAPI.Tests.Infrastructure;

/// <summary>
/// Base para testes de integração: cria uma CustomWebApplicationFactory e um
/// HttpClient novos por teste (xUnit instancia a classe de teste uma vez por
/// método), garantindo isolamento total do banco SQLite em memória entre
/// testes — nenhum precisa se preocupar em limpar dado deixado por outro.
/// </summary>
public abstract class IntegrationTestBase : IAsyncLifetime
{
    protected CustomWebApplicationFactory Factory { get; private set; } = null!;
    protected HttpClient Client { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        Factory = new CustomWebApplicationFactory();
        await Factory.InicializarBancoAsync();
        Client = Factory.CreateClient();
    }

    public Task DisposeAsync()
    {
        Client.Dispose();
        Factory.Dispose();
        return Task.CompletedTask;
    }
}
