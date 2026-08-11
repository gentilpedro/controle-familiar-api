using System.Net;
using System.Net.Http.Json;
using ControleFamiliarAPI.DTOs.Pessoa;
using ControleFamiliarAPI.Middlewares;
using ControleFamiliarAPI.Services;
using ControleFamiliarAPI.Tests.Infrastructure;

namespace ControleFamiliarAPI.Tests.Integration;

/// <summary>
/// Cobre a sessão em cookie HttpOnly e a proteção CSRF que ela exige.
/// </summary>
public class CookieDeSessaoTests : IntegrationTestBase
{
    private static string? CabecalhoDoCookie(HttpResponseMessage response) =>
        response.Headers.TryGetValues("Set-Cookie", out var valores)
            ? valores.FirstOrDefault(v => v.StartsWith(CookieDeSessao.Nome, StringComparison.Ordinal))
            : null;

    [Fact]
    public async Task Login_GravaOCookieComoHttpOnlyESameSiteLax()
    {
        await AuthTestHelper.RegistrarNovaFamiliaAsync(Client, email: "cookie@teste.com", senha: "SenhaCerta1");

        var response = await Client.PostAsJsonAsync(
            "/api/auth/login",
            new { email = "cookie@teste.com", senha = "SenhaCerta1" },
            AuthTestHelper.JsonOptions);

        response.EnsureSuccessStatusCode();

        var cookie = CabecalhoDoCookie(response);

        Assert.NotNull(cookie);
        // httponly é o ponto da mudança: sem ele o JavaScript leria a sessão.
        Assert.Contains("httponly", cookie!, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=lax", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("path=/", cookie, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Registrar_TambemJaDeixaASessaoNoCookie()
    {
        var response = await Client.PostAsJsonAsync(
            "/api/auth/registrar",
            new
            {
                nome = "Novo",
                email = $"{Guid.NewGuid():N}@teste.com",
                senha = "Senha123",
                modoFamilia = "Nova"
            },
            AuthTestHelper.JsonOptions);

        response.EnsureSuccessStatusCode();

        Assert.NotNull(CabecalhoDoCookie(response));
    }

    // O ponto central: autenticar sem nunca mandar Authorization.
    [Fact]
    public async Task RotaProtegida_AutenticaSomenteComOCookie()
    {
        var auth = await AuthTestHelper.RegistrarNovaFamiliaAsync(Client);

        var requisicao = new HttpRequestMessage(HttpMethod.Get, "/api/pessoas");
        requisicao.Headers.Add("Cookie", $"{CookieDeSessao.Nome}={auth.Token}");

        var response = await Client.SendAsync(requisicao);

        response.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Logout_ApagaOCookie()
    {
        var auth = await AuthTestHelper.RegistrarNovaFamiliaAsync(Client);

        var requisicao = new HttpRequestMessage(HttpMethod.Post, "/api/auth/logout");
        requisicao.Headers.Add("Cookie", $"{CookieDeSessao.Nome}={auth.Token}");
        requisicao.Headers.Add(CsrfMiddleware.NomeHeader, "1");

        var response = await Client.SendAsync(requisicao);
        response.EnsureSuccessStatusCode();

        var cookie = CabecalhoDoCookie(response);

        // O jeito de apagar um cookie é reenviá-lo vazio e com data no passado.
        Assert.NotNull(cookie);
        Assert.Contains("expires=", cookie!, StringComparison.OrdinalIgnoreCase);
    }

    // --- CSRF ---

    [Fact]
    public async Task EscritaComCookieSemOHeader_Retorna403()
    {
        var auth = await AuthTestHelper.RegistrarNovaFamiliaAsync(Client);

        var requisicao = new HttpRequestMessage(HttpMethod.Post, "/api/pessoas")
        {
            Content = JsonContent.Create(new PessoaCreateDto { Nome = "Alguém", Idade = 30 })
        };
        requisicao.Headers.Add("Cookie", $"{CookieDeSessao.Nome}={auth.Token}");

        var response = await Client.SendAsync(requisicao);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task EscritaComCookieEComOHeader_Passa()
    {
        var auth = await AuthTestHelper.RegistrarNovaFamiliaAsync(Client);

        var requisicao = new HttpRequestMessage(HttpMethod.Post, "/api/pessoas")
        {
            Content = JsonContent.Create(new PessoaCreateDto { Nome = "Alguém", Idade = 30 })
        };
        requisicao.Headers.Add("Cookie", $"{CookieDeSessao.Nome}={auth.Token}");
        requisicao.Headers.Add(CsrfMiddleware.NomeHeader, "1");

        var response = await Client.SendAsync(requisicao);

        response.EnsureSuccessStatusCode();
    }

    // Quem usa Bearer (Scalar, curl, Postman) é imune a CSRF por construção —
    // exigir o header ali só quebraria essas ferramentas sem ganho nenhum.
    [Fact]
    public async Task EscritaComBearerSemOHeader_ContinuaPassando()
    {
        var auth = await AuthTestHelper.RegistrarNovaFamiliaAsync(Client);
        Client.ComToken(auth.Token);

        var response = await Client.PostAsJsonAsync(
            "/api/pessoas",
            new PessoaCreateDto { Nome = "Alguém", Idade = 30 },
            AuthTestHelper.JsonOptions);

        response.EnsureSuccessStatusCode();
    }

    // Regressão: o OnMessageReceived roda antes de o handler ler o
    // Authorization, então checar context.Token ali não distingue nada e o
    // cookie acabava sobrescrevendo o Bearer — a requisição autenticava como o
    // dono do cookie, não como o dono do token enviado explicitamente.
    [Fact]
    public async Task ComCookieEBearerDeUsuariosDiferentes_ValeOBearer()
    {
        var primeiro = await AuthTestHelper.RegistrarNovaFamiliaAsync(Client, email: $"{Guid.NewGuid():N}@teste.com");
        var segundo = await AuthTestHelper.RegistrarNovaFamiliaAsync(Client, email: $"{Guid.NewGuid():N}@teste.com");

        var requisicao = new HttpRequestMessage(HttpMethod.Get, "/api/auth/me");
        requisicao.Headers.Add("Cookie", $"{CookieDeSessao.Nome}={primeiro.Token}");
        requisicao.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", segundo.Token);

        var response = await Client.SendAsync(requisicao);
        response.EnsureSuccessStatusCode();

        var corpo = await response.Content.ReadAsStringAsync();

        Assert.Contains(segundo.Usuario.Email, corpo);
        Assert.DoesNotContain(primeiro.Usuario.Email, corpo);
    }

    [Fact]
    public async Task LeituraComCookieSemOHeader_Passa()
    {
        var auth = await AuthTestHelper.RegistrarNovaFamiliaAsync(Client);

        var requisicao = new HttpRequestMessage(HttpMethod.Get, "/api/pessoas");
        requisicao.Headers.Add("Cookie", $"{CookieDeSessao.Nome}={auth.Token}");

        var response = await Client.SendAsync(requisicao);

        response.EnsureSuccessStatusCode();
    }
}
