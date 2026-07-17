using System.Net;
using System.Net.Http.Json;
using ControleFamiliarAPI.DTOs.Pessoa;
using ControleFamiliarAPI.Responses;
using ControleFamiliarAPI.Tests.Infrastructure;

namespace ControleFamiliarAPI.Tests.Integration;

public class PessoasTests : IntegrationTestBase
{
    /// <summary>
    /// Regressão do bug corrigido no Bloco 2: PessoaUpdateDto tinha [Required]
    /// em Nome e Idade, rejeitando com 400 qualquer PATCH que só enviasse um
    /// dos dois campos — mesmo o service sendo escrito para atualização
    /// parcial. Idade = null aqui reproduz exatamente esse cenário.
    /// </summary>
    [Fact]
    public async Task Patch_EnviandoSoNome_AtualizaSoNomeENaoRejeitaComoInvalido()
    {
        var auth = await AuthTestHelper.RegistrarNovaFamiliaAsync(Client);
        Client.ComToken(auth.Token);

        var criarResponse = await Client.PostAsJsonAsync("/api/pessoas", new PessoaCreateDto { Nome = "Ana", Idade = 10 }, AuthTestHelper.JsonOptions);
        criarResponse.EnsureSuccessStatusCode();
        var criada = await criarResponse.Content.ReadFromJsonAsync<ApiResponse<PessoaResponseDto>>(AuthTestHelper.JsonOptions);
        var id = criada!.Data!.Id;

        var patchResponse = await Client.PatchAsJsonAsync(
            $"/api/pessoas/{id}",
            new PessoaUpdateDto { Nome = "Ana Paula", Idade = null },
            AuthTestHelper.JsonOptions);

        Assert.Equal(HttpStatusCode.OK, patchResponse.StatusCode);

        var listaResponse = await Client.GetAsync("/api/pessoas");
        var lista = await listaResponse.Content.ReadFromJsonAsync<List<PessoaResponseDto>>(AuthTestHelper.JsonOptions);
        var atualizada = lista!.Single(p => p.Id == id);

        Assert.Equal("Ana Paula", atualizada.Nome);
        Assert.Equal(10, atualizada.Idade); // idade não enviada no PATCH -> não deve mudar
    }
}
