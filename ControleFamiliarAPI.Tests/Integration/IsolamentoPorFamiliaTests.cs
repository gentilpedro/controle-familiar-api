using System.Net;
using System.Net.Http.Json;
using ControleFamiliarAPI.DTOs.Pessoa;
using ControleFamiliarAPI.Responses;
using ControleFamiliarAPI.Tests.Infrastructure;

namespace ControleFamiliarAPI.Tests.Integration;

public class IsolamentoPorFamiliaTests : IntegrationTestBase
{
    [Fact]
    public async Task Pessoas_UsuarioNaoVePessoasDeOutraFamilia()
    {
        var usuarioA = await AuthTestHelper.RegistrarNovaFamiliaAsync(Client, email: "familia-a@teste.com");
        Client.ComToken(usuarioA.Token);
        var criarResponse = await Client.PostAsJsonAsync("/api/pessoas", new PessoaCreateDto { Nome = "Pessoa da Família A", Idade = 40 }, AuthTestHelper.JsonOptions);
        criarResponse.EnsureSuccessStatusCode();

        using var clienteB = Factory.CreateClient();
        var usuarioB = await AuthTestHelper.RegistrarNovaFamiliaAsync(clienteB, email: "familia-b@teste.com");
        clienteB.ComToken(usuarioB.Token);

        var listaB = await clienteB.GetFromJsonAsync<List<PessoaResponseDto>>("/api/pessoas", AuthTestHelper.JsonOptions);

        // B não vê lista vazia: toda conta nasce com a pessoa do próprio
        // titular. O que o isolamento garante é que nada da família A apareça.
        Assert.DoesNotContain(listaB!, p => p.Nome == "Pessoa da Família A");
        Assert.Single(listaB!);
        Assert.True(listaB![0].EhMembro);
    }

    [Fact]
    public async Task Pessoas_DeletarPessoaDeOutraFamilia_Retorna404()
    {
        var usuarioA = await AuthTestHelper.RegistrarNovaFamiliaAsync(Client, email: "dono@teste.com");
        Client.ComToken(usuarioA.Token);
        var criarResponse = await Client.PostAsJsonAsync("/api/pessoas", new PessoaCreateDto { Nome = "Pessoa da Família A", Idade = 40 }, AuthTestHelper.JsonOptions);
        var criada = await criarResponse.Content.ReadFromJsonAsync<ApiResponse<PessoaResponseDto>>(AuthTestHelper.JsonOptions);
        var pessoaIdDaFamiliaA = criada!.Data!.Id;

        using var clienteB = Factory.CreateClient();
        var usuarioB = await AuthTestHelper.RegistrarNovaFamiliaAsync(clienteB, email: "invasor@teste.com");
        clienteB.ComToken(usuarioB.Token);

        var deleteResponse = await clienteB.DeleteAsync($"/api/pessoas/{pessoaIdDaFamiliaA}");

        Assert.Equal(HttpStatusCode.NotFound, deleteResponse.StatusCode);
    }
}
