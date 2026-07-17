using System.Net;
using System.Net.Http.Json;
using ControleFamiliarAPI.DTOs.Auth;
using ControleFamiliarAPI.Tests.Infrastructure;

namespace ControleFamiliarAPI.Tests.Integration;

public class AuthTests : IntegrationTestBase
{
    [Fact]
    public async Task Registrar_ComModoFamiliaNova_CriaContaERetornaToken()
    {
        var auth = await AuthTestHelper.RegistrarNovaFamiliaAsync(Client);

        Assert.False(string.IsNullOrWhiteSpace(auth.Token));
        Assert.True(auth.Usuario.EhAdministrador);
        Assert.NotEmpty(auth.Familia.CodigoConvite);
    }

    [Fact]
    public async Task Registrar_ComModoFamiliaInvalido_Retorna400()
    {
        var dto = new RegistrarDto
        {
            Nome = "Usuário Teste",
            Email = $"{Guid.NewGuid():N}@teste.com",
            Senha = "Senha123",
            ModoFamilia = "ModoQueNaoExiste"
        };

        var response = await Client.PostAsJsonAsync("/api/auth/registrar", dto, AuthTestHelper.JsonOptions);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Login_ComSenhaErrada_Retorna401()
    {
        var auth = await AuthTestHelper.RegistrarNovaFamiliaAsync(Client, email: "login-errado@teste.com", senha: "SenhaCorreta1");

        var login = new LoginDto { Email = "login-errado@teste.com", Senha = "SenhaErrada1" };
        var response = await Client.PostAsJsonAsync("/api/auth/login", login, AuthTestHelper.JsonOptions);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.NotNull(auth); // só pra confirmar que o registro anterior funcionou
    }

    [Fact]
    public async Task Login_ComCredenciaisCorretas_RetornaToken()
    {
        await AuthTestHelper.RegistrarNovaFamiliaAsync(Client, email: "login-ok@teste.com", senha: "SenhaCorreta1");

        var login = new LoginDto { Email = "login-ok@teste.com", Senha = "SenhaCorreta1" };
        var response = await Client.PostAsJsonAsync("/api/auth/login", login, AuthTestHelper.JsonOptions);

        response.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task EndpointProtegido_SemToken_Retorna401()
    {
        var response = await Client.GetAsync("/api/pessoas");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
