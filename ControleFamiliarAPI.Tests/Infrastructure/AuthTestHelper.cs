using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using ControleFamiliarAPI.DTOs.Auth;
using ControleFamiliarAPI.Models.Enums;
using ControleFamiliarAPI.Responses;

namespace ControleFamiliarAPI.Tests.Infrastructure;

public static class AuthTestHelper
{
    public static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// Registra um usuário novo criando uma família nova e devolve a
    /// resposta de autenticação (token, usuário, família) já pronta.
    /// </summary>
    public static async Task<AuthResponseDto> RegistrarNovaFamiliaAsync(
        HttpClient client,
        string? nome = null,
        string? email = null,
        string senha = "Senha123",
        int idade = 30)
    {
        var dto = new RegistrarDto
        {
            Nome = nome ?? "Usuário Teste",
            Email = email ?? $"{Guid.NewGuid():N}@teste.com",
            Senha = senha,
            Idade = idade,
            ModoFamilia = ModoEntradaFamilia.Nova,
            NomeFamilia = "Família Teste"
        };

        var response = await client.PostAsJsonAsync("/api/auth/registrar", dto, JsonOptions);
        response.EnsureSuccessStatusCode();

        var envelope = await response.Content.ReadFromJsonAsync<ApiResponse<AuthResponseDto>>(JsonOptions);
        return envelope!.Data!;
    }

    public static HttpClient ComToken(this HttpClient client, string token)
    {
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }
}
