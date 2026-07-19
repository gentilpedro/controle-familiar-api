using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using ControleFamiliarAPI.Data;
using ControleFamiliarAPI.DTOs.Auth;
using ControleFamiliarAPI.Models.Enums;
using ControleFamiliarAPI.Responses;
using Microsoft.Extensions.DependencyInjection;

namespace ControleFamiliarAPI.Tests.Infrastructure;

public static class AuthTestHelper
{
    public static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// Registra um usuário novo criando uma família nova e devolve a
    /// resposta de autenticação (token, usuário, família) já pronta.
    ///
    /// Já sai com a assinatura Individual marcada como Ativa direto no
    /// banco - sem isso, todo teste que bate em Pessoas/Categorias/
    /// Transacoes/Relatorios cairia no 402 do [ExigirAssinatura], já que
    /// StatusAssinaturaIndividual nasce como Nenhuma. Simula o estado "já
    /// pagante" sem precisar passar pelo Stripe de verdade em teste.
    /// </summary>
    public static async Task<AuthResponseDto> RegistrarNovaFamiliaAsync(
        CustomWebApplicationFactory factory,
        HttpClient client,
        string? nome = null,
        string? email = null,
        string senha = "Senha123")
    {
        var dto = new RegistrarDto
        {
            Nome = nome ?? "Usuário Teste",
            Email = email ?? $"{Guid.NewGuid():N}@teste.com",
            Senha = senha,
            ModoFamilia = "Nova",
            NomeFamilia = "Família Teste"
        };

        var response = await client.PostAsJsonAsync("/api/auth/registrar", dto, JsonOptions);
        response.EnsureSuccessStatusCode();

        var envelope = await response.Content.ReadFromJsonAsync<ApiResponse<AuthResponseDto>>(JsonOptions);
        var resultado = envelope!.Data!;

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var usuario = await db.Usuarios.FindAsync(resultado.Usuario.Id);
            usuario!.StatusAssinaturaIndividual = StatusAssinatura.Ativa;
            await db.SaveChangesAsync();
        }

        return resultado;
    }

    public static HttpClient ComToken(this HttpClient client, string token)
    {
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }
}
