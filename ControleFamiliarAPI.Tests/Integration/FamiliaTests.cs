using System.Net;
using ControleFamiliarAPI.Tests.Infrastructure;

namespace ControleFamiliarAPI.Tests.Integration;

public class FamiliaTests : IntegrationTestBase
{
    /// <summary>
    /// Regressão do fix do Bloco 4: RebaixarAdmin virou um ExecuteUpdateAsync
    /// condicional atômico especificamente para impedir que o último admin
    /// de uma família seja rebaixado, deixando a família sem administrador.
    /// </summary>
    [Fact]
    public async Task Rebaixar_UnicoEUltimoAdministrador_Retorna400()
    {
        var auth = await AuthTestHelper.RegistrarNovaFamiliaAsync(Client);
        Client.ComToken(auth.Token);

        var response = await Client.PostAsync($"/api/familia/membros/{auth.Usuario.Id}/rebaixar", null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task RemoverMembro_ASiMesmo_Retorna400()
    {
        var auth = await AuthTestHelper.RegistrarNovaFamiliaAsync(Client);
        Client.ComToken(auth.Token);

        var response = await Client.DeleteAsync($"/api/familia/membros/{auth.Usuario.Id}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
