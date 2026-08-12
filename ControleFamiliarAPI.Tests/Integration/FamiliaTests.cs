using System.Net;
using System.Net.Http.Json;
using ControleFamiliarAPI.Data;
using ControleFamiliarAPI.DTOs.Auth;
using ControleFamiliarAPI.Models.Enums;
using ControleFamiliarAPI.Responses;
using ControleFamiliarAPI.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

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

    /// <summary>
    /// LGPD, art. 37 (bloco 4): operações sensíveis sobre membros da família
    /// precisam ficar registradas numa trilha de auditoria.
    /// </summary>
    [Fact]
    public async Task RemoverMembro_RegistraAuditoria()
    {
        var admin = await AuthTestHelper.RegistrarNovaFamiliaAsync(Client, email: $"{Guid.NewGuid():N}@teste.com");

        var membroDto = new RegistrarDto
        {
            Nome = "Membro Comum",
            Email = $"{Guid.NewGuid():N}@teste.com",
            Senha = "Senha123",
            Idade = 30,
            ModoFamilia = ModoEntradaFamilia.Entrar,
            CodigoConvite = admin.Familia.CodigoConvite
        };
        var membroResponse = await Client.PostAsJsonAsync("/api/auth/registrar", membroDto, AuthTestHelper.JsonOptions);
        membroResponse.EnsureSuccessStatusCode();
        var membroEnvelope = await membroResponse.Content.ReadFromJsonAsync<ApiResponse<AuthResponseDto>>(AuthTestHelper.JsonOptions);
        var membroId = membroEnvelope!.Data!.Usuario.Id;

        Client.ComToken(admin.Token);

        var removerResponse = await Client.DeleteAsync($"/api/familia/membros/{membroId}");
        removerResponse.EnsureSuccessStatusCode();

        using var scope = Factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var registro = await context.RegistrosAuditoria
            .SingleOrDefaultAsync(r => r.Acao == "RemocaoMembro" && r.UsuarioAlvoId == membroId);

        Assert.NotNull(registro);
        Assert.Equal(admin.Usuario.Id, registro!.UsuarioId);
        Assert.Equal("Membro Comum", registro.NomeAlvo);
    }

    /// <summary>
    /// Base do Relatório Familiar no front: criação e entrada de membro
    /// aparecem no histórico, com o nome de cada um no momento do evento.
    /// </summary>
    [Fact]
    public async Task Historico_ContemCriacaoDaFamiliaEEntradaDeMembro()
    {
        var admin = await AuthTestHelper.RegistrarNovaFamiliaAsync(Client, nome: "Fundadora", email: $"{Guid.NewGuid():N}@teste.com");

        var membroDto = new RegistrarDto
        {
            Nome = "Novo Membro",
            Email = $"{Guid.NewGuid():N}@teste.com",
            Senha = "Senha123",
            Idade = 25,
            ModoFamilia = ModoEntradaFamilia.Entrar,
            CodigoConvite = admin.Familia.CodigoConvite
        };
        (await Client.PostAsJsonAsync("/api/auth/registrar", membroDto, AuthTestHelper.JsonOptions)).EnsureSuccessStatusCode();

        Client.ComToken(admin.Token);

        var response = await Client.GetAsync("/api/familia/historico");
        response.EnsureSuccessStatusCode();
        var envelope = await response.Content.ReadFromJsonAsync<ApiResponse<List<HistoricoFamiliaItemDto>>>(AuthTestHelper.JsonOptions);
        var historico = envelope!.Data!;

        Assert.Contains(historico, h => h.Acao == "CriacaoFamilia" && h.NomeAlvo == "Fundadora");
        Assert.Contains(historico, h => h.Acao == "EntradaFamilia" && h.NomeAlvo == "Novo Membro");

        // Mais recente primeiro: quem entrou depois aparece antes de quem
        // fundou a família.
        var indiceEntrada = historico.FindIndex(h => h.Acao == "EntradaFamilia");
        var indiceCriacao = historico.FindIndex(h => h.Acao == "CriacaoFamilia");
        Assert.True(indiceEntrada < indiceCriacao);
    }

    /// <summary>
    /// O histórico é um recorte de RegistroAuditoria — só entra/sai. Promoção
    /// de admin também vai pra auditoria, mas não conta a história de quem
    /// esteve na família, então fica de fora deste endpoint.
    /// </summary>
    [Fact]
    public async Task Historico_NaoContemPromocaoDeAdmin()
    {
        var admin = await AuthTestHelper.RegistrarNovaFamiliaAsync(Client, email: $"{Guid.NewGuid():N}@teste.com");

        var membroDto = new RegistrarDto
        {
            Nome = "Membro Comum",
            Email = $"{Guid.NewGuid():N}@teste.com",
            Senha = "Senha123",
            Idade = 30,
            ModoFamilia = ModoEntradaFamilia.Entrar,
            CodigoConvite = admin.Familia.CodigoConvite
        };
        var membroResponse = await Client.PostAsJsonAsync("/api/auth/registrar", membroDto, AuthTestHelper.JsonOptions);
        var membroEnvelope = await membroResponse.Content.ReadFromJsonAsync<ApiResponse<AuthResponseDto>>(AuthTestHelper.JsonOptions);
        var membroId = membroEnvelope!.Data!.Usuario.Id;

        Client.ComToken(admin.Token);
        (await Client.PostAsync($"/api/familia/membros/{membroId}/promover", null)).EnsureSuccessStatusCode();

        var response = await Client.GetAsync("/api/familia/historico");
        var envelope = await response.Content.ReadFromJsonAsync<ApiResponse<List<HistoricoFamiliaItemDto>>>(AuthTestHelper.JsonOptions);

        Assert.DoesNotContain(envelope!.Data!, h => h.Acao == "PromocaoAdmin");
    }
}
