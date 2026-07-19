using System.Net;
using System.Net.Http.Json;
using ControleFamiliarAPI.DTOs.Auth;
using ControleFamiliarAPI.Responses;
using ControleFamiliarAPI.Tests.Infrastructure;

namespace ControleFamiliarAPI.Tests.Integration;

public class AssinaturaPaywallTests : IntegrationTestBase
{
    /// <summary>
    /// [ExigirAssinatura] (Bloco 4): rotas financeiras devolvem 402 pra quem
    /// não tem assinatura ativa nem em teste. Registra direto (sem passar
    /// pelo AuthTestHelper, que grants a assinatura de propósito pros
    /// outros testes não precisarem se preocupar com isso) pra reproduzir o
    /// estado real de uma conta recém-criada.
    /// </summary>
    [Fact]
    public async Task EndpointFinanceiro_SemAssinaturaAtiva_Retorna402()
    {
        var dto = new RegistrarDto
        {
            Nome = "Sem Assinatura",
            Email = $"{Guid.NewGuid():N}@teste.com",
            Senha = "Senha123",
            ModoFamilia = "Nova",
            NomeFamilia = "Família Sem Assinatura"
        };
        var registroResponse = await Client.PostAsJsonAsync("/api/auth/registrar", dto, AuthTestHelper.JsonOptions);
        registroResponse.EnsureSuccessStatusCode();
        var envelope = await registroResponse.Content.ReadFromJsonAsync<ApiResponse<AuthResponseDto>>(AuthTestHelper.JsonOptions);
        Client.ComToken(envelope!.Data!.Token);

        var response = await Client.GetAsync("/api/pessoas");

        Assert.Equal(HttpStatusCode.PaymentRequired, response.StatusCode);
    }

    /// <summary>
    /// AuthService.EntrarEmFamilia (Bloco 4): teto de 5 membros por família
    /// do plano Família - o 6º cadastro pelo mesmo código de convite deve
    /// ser rejeitado.
    /// </summary>
    [Fact]
    public async Task EntrarNaFamilia_ComCincoMembros_RejeitaOSexto()
    {
        var admin = await AuthTestHelper.RegistrarNovaFamiliaAsync(Factory, Client, email: $"{Guid.NewGuid():N}@teste.com");

        for (var i = 0; i < 4; i++)
        {
            var membroDto = new RegistrarDto
            {
                Nome = $"Membro {i}",
                Email = $"{Guid.NewGuid():N}@teste.com",
                Senha = "Senha123",
                ModoFamilia = "Entrar",
                CodigoConvite = admin.Familia.CodigoConvite
            };
            var membroResponse = await Client.PostAsJsonAsync("/api/auth/registrar", membroDto, AuthTestHelper.JsonOptions);
            membroResponse.EnsureSuccessStatusCode();
        }

        var sextoDto = new RegistrarDto
        {
            Nome = "Sexto Membro",
            Email = $"{Guid.NewGuid():N}@teste.com",
            Senha = "Senha123",
            ModoFamilia = "Entrar",
            CodigoConvite = admin.Familia.CodigoConvite
        };
        var sextoResponse = await Client.PostAsJsonAsync("/api/auth/registrar", sextoDto, AuthTestHelper.JsonOptions);

        Assert.Equal(HttpStatusCode.BadRequest, sextoResponse.StatusCode);
    }
}
