using System.Net;
using System.Net.Http.Json;
using ControleFamiliarAPI.DTOs.Categoria;
using ControleFamiliarAPI.DTOs.Paginacao;
using ControleFamiliarAPI.DTOs.Pessoa;
using ControleFamiliarAPI.DTOs.Transacao;
using ControleFamiliarAPI.Models.Enums;
using ControleFamiliarAPI.Responses;
using ControleFamiliarAPI.Tests.Infrastructure;

namespace ControleFamiliarAPI.Tests.Integration;

public class TransacoesTests : IntegrationTestBase
{
    private async Task<(int PessoaId, int CategoriaId)> CriarPessoaECategoriaAsync(int idadePessoa, FinalidadeCategoria finalidadeCategoria)
    {
        var pessoaResponse = await Client.PostAsJsonAsync("/api/pessoas", new PessoaCreateDto { Nome = "Pessoa Teste", Idade = idadePessoa }, AuthTestHelper.JsonOptions);
        var pessoa = await pessoaResponse.Content.ReadFromJsonAsync<ApiResponse<PessoaResponseDto>>(AuthTestHelper.JsonOptions);

        var categoriaResponse = await Client.PostAsJsonAsync("/api/categorias", new CategoriaCreateDto { Descricao = "Categoria Teste", Finalidade = finalidadeCategoria }, AuthTestHelper.JsonOptions);
        var categoria = await categoriaResponse.Content.ReadFromJsonAsync<CategoriaResponseDto>(AuthTestHelper.JsonOptions);

        return (pessoa!.Data!.Id, categoria!.Id);
    }

    [Fact]
    public async Task Criar_ComMenorDeIdadeERegistrandoReceita_Retorna400()
    {
        var auth = await AuthTestHelper.RegistrarNovaFamiliaAsync(Factory, Client);
        Client.ComToken(auth.Token);

        var (pessoaId, categoriaId) = await CriarPessoaECategoriaAsync(idadePessoa: 15, FinalidadeCategoria.Ambas);

        var dto = new TransacaoCreateDto { Descricao = "Mesada", Valor = 50, Tipo = TipoTransacao.Receita, PessoaId = pessoaId, CategoriaId = categoriaId };
        var response = await Client.PostAsJsonAsync("/api/transacoes", dto, AuthTestHelper.JsonOptions);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Criar_ComCategoriaIncompativelComOTipo_Retorna400()
    {
        var auth = await AuthTestHelper.RegistrarNovaFamiliaAsync(Factory, Client);
        Client.ComToken(auth.Token);

        var (pessoaId, categoriaId) = await CriarPessoaECategoriaAsync(idadePessoa: 30, FinalidadeCategoria.Despesa);

        var dto = new TransacaoCreateDto { Descricao = "Salário", Valor = 1000, Tipo = TipoTransacao.Receita, PessoaId = pessoaId, CategoriaId = categoriaId };
        var response = await Client.PostAsJsonAsync("/api/transacoes", dto, AuthTestHelper.JsonOptions);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Criar_ComDadosValidos_CriaEApareceNaListagem()
    {
        var auth = await AuthTestHelper.RegistrarNovaFamiliaAsync(Factory, Client);
        Client.ComToken(auth.Token);

        var (pessoaId, categoriaId) = await CriarPessoaECategoriaAsync(idadePessoa: 30, FinalidadeCategoria.Ambas);

        var dto = new TransacaoCreateDto { Descricao = "Salário", Valor = 1000, Tipo = TipoTransacao.Receita, PessoaId = pessoaId, CategoriaId = categoriaId };
        var criarResponse = await Client.PostAsJsonAsync("/api/transacoes", dto, AuthTestHelper.JsonOptions);
        criarResponse.EnsureSuccessStatusCode();

        var listaResponse = await Client.GetAsync("/api/transacoes?pagina=1&tamanhoPagina=50");
        listaResponse.EnsureSuccessStatusCode();
        var pagina = await listaResponse.Content.ReadFromJsonAsync<PaginacaoResultado<TransacaoResponseDto>>(AuthTestHelper.JsonOptions);

        Assert.Equal(1, pagina!.TotalItens);
        Assert.Single(pagina.Itens);
    }
}
