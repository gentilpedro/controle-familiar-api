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

    /// <summary>
    /// Sem isto, quem acabava de se cadastrar caía num painel sem pessoa
    /// nenhuma — e como toda transação exige uma pessoa, não dava para lançar
    /// nada até descobrir sozinho que precisava cadastrar a si mesmo.
    /// </summary>
    [Fact]
    public async Task Registrar_CriaAPessoaDoTitularJaMarcadaComoMembro()
    {
        var auth = await AuthTestHelper.RegistrarNovaFamiliaAsync(Client, nome: "Pedro", idade: 34);
        Client.ComToken(auth.Token);

        var listaResponse = await Client.GetAsync("/api/pessoas");
        listaResponse.EnsureSuccessStatusCode();
        var lista = await listaResponse.Content.ReadFromJsonAsync<List<PessoaResponseDto>>(AuthTestHelper.JsonOptions);

        var titular = Assert.Single(lista!);

        Assert.Equal("Pedro", titular.Nome);
        Assert.Equal(34, titular.Idade);
        Assert.True(titular.EhMembro);
    }

    /// <summary>
    /// Pessoa cadastrada à mão (dependente sem login) continua excluível — é o
    /// contraste que garante que o bloqueio abaixo é sobre o vínculo com a
    /// conta, e não sobre a exclusão ter sido desligada de vez.
    /// </summary>
    [Fact]
    public async Task Deletar_PessoaCadastradaAMao_Funciona()
    {
        var auth = await AuthTestHelper.RegistrarNovaFamiliaAsync(Client);
        Client.ComToken(auth.Token);

        var criarResponse = await Client.PostAsJsonAsync("/api/pessoas", new PessoaCreateDto { Nome = "Filho", Idade = 8 }, AuthTestHelper.JsonOptions);
        criarResponse.EnsureSuccessStatusCode();
        var criada = await criarResponse.Content.ReadFromJsonAsync<ApiResponse<PessoaResponseDto>>(AuthTestHelper.JsonOptions);

        Assert.False(criada!.Data!.EhMembro);

        var deleteResponse = await Client.DeleteAsync($"/api/pessoas/{criada.Data.Id}");

        Assert.Equal(HttpStatusCode.OK, deleteResponse.StatusCode);
    }

    /// <summary>
    /// Excluir por aqui deixaria um membro ativo da família sem pessoa nenhuma
    /// para lançar despesa. Quem representa uma conta só sai junto com ela.
    /// </summary>
    [Fact]
    public async Task Deletar_PessoaDeUmMembro_Retorna400()
    {
        var auth = await AuthTestHelper.RegistrarNovaFamiliaAsync(Client);
        Client.ComToken(auth.Token);

        var listaResponse = await Client.GetAsync("/api/pessoas");
        var lista = await listaResponse.Content.ReadFromJsonAsync<List<PessoaResponseDto>>(AuthTestHelper.JsonOptions);
        var titular = lista!.Single(p => p.EhMembro);

        var deleteResponse = await Client.DeleteAsync($"/api/pessoas/{titular.Id}");

        Assert.Equal(HttpStatusCode.BadRequest, deleteResponse.StatusCode);
    }
}
