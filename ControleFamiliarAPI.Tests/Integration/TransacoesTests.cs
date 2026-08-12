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
        var auth = await AuthTestHelper.RegistrarNovaFamiliaAsync(Client);
        Client.ComToken(auth.Token);

        var (pessoaId, categoriaId) = await CriarPessoaECategoriaAsync(idadePessoa: 15, FinalidadeCategoria.Ambas);

        var dto = new TransacaoCreateDto { Descricao = "Mesada", Valor = 50, Tipo = TipoTransacao.Receita, Data = DateOnly.FromDateTime(DateTime.UtcNow), PessoaId = pessoaId, CategoriaId = categoriaId };
        var response = await Client.PostAsJsonAsync("/api/transacoes", dto, AuthTestHelper.JsonOptions);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Criar_ComCategoriaIncompativelComOTipo_Retorna400()
    {
        var auth = await AuthTestHelper.RegistrarNovaFamiliaAsync(Client);
        Client.ComToken(auth.Token);

        var (pessoaId, categoriaId) = await CriarPessoaECategoriaAsync(idadePessoa: 30, FinalidadeCategoria.Despesa);

        var dto = new TransacaoCreateDto { Descricao = "Salário", Valor = 1000, Tipo = TipoTransacao.Receita, Data = DateOnly.FromDateTime(DateTime.UtcNow), PessoaId = pessoaId, CategoriaId = categoriaId };
        var response = await Client.PostAsJsonAsync("/api/transacoes", dto, AuthTestHelper.JsonOptions);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Criar_ComDadosValidos_CriaEApareceNaListagem()
    {
        var auth = await AuthTestHelper.RegistrarNovaFamiliaAsync(Client);
        Client.ComToken(auth.Token);

        var (pessoaId, categoriaId) = await CriarPessoaECategoriaAsync(idadePessoa: 30, FinalidadeCategoria.Ambas);
        var data = DateOnly.FromDateTime(DateTime.UtcNow);

        var dto = new TransacaoCreateDto { Descricao = "Salário", Valor = 1000, Tipo = TipoTransacao.Receita, Data = data, PessoaId = pessoaId, CategoriaId = categoriaId };
        var criarResponse = await Client.PostAsJsonAsync("/api/transacoes", dto, AuthTestHelper.JsonOptions);
        criarResponse.EnsureSuccessStatusCode();

        var listaResponse = await Client.GetAsync("/api/transacoes?pagina=1&tamanhoPagina=50");
        listaResponse.EnsureSuccessStatusCode();
        var pagina = await listaResponse.Content.ReadFromJsonAsync<PaginacaoResultado<TransacaoResponseDto>>(AuthTestHelper.JsonOptions);

        Assert.Equal(1, pagina!.TotalItens);
        var transacao = Assert.Single(pagina.Itens);
        Assert.Equal(data, transacao.Data);
    }

    /// <summary>
    /// Idade não-nula é [Required] em DateOnly? (nullable de propósito): sem
    /// isso, omitir o campo cairia silenciosamente em 0001-01-01 em vez de
    /// dar 400 — mesmo raciocínio de RegistrarDto.Idade.
    /// </summary>
    [Fact]
    public async Task Criar_SemData_Retorna400()
    {
        var auth = await AuthTestHelper.RegistrarNovaFamiliaAsync(Client);
        Client.ComToken(auth.Token);

        var (pessoaId, categoriaId) = await CriarPessoaECategoriaAsync(idadePessoa: 30, FinalidadeCategoria.Ambas);

        var dto = new TransacaoCreateDto { Descricao = "Salário", Valor = 1000, Tipo = TipoTransacao.Receita, PessoaId = pessoaId, CategoriaId = categoriaId };
        var response = await Client.PostAsJsonAsync("/api/transacoes", dto, AuthTestHelper.JsonOptions);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    /// <summary>
    /// Listar ordena por Data DESC, não por Id — uma transação lançada hoje
    /// com data passada não deve furar na frente de uma mais antiga com data
    /// mais recente.
    /// </summary>
    [Fact]
    public async Task Listar_OrdenaPorDataDescendente()
    {
        var auth = await AuthTestHelper.RegistrarNovaFamiliaAsync(Client);
        Client.ComToken(auth.Token);

        var (pessoaId, categoriaId) = await CriarPessoaECategoriaAsync(idadePessoa: 30, FinalidadeCategoria.Ambas);
        var hoje = DateOnly.FromDateTime(DateTime.UtcNow);

        // Criada primeiro (Id menor) mas com data futura — deve aparecer
        // antes da segunda, que tem Id maior mas data mais antiga.
        var maisRecente = new TransacaoCreateDto { Descricao = "Futura", Valor = 10, Tipo = TipoTransacao.Receita, Data = hoje.AddDays(5), PessoaId = pessoaId, CategoriaId = categoriaId };
        (await Client.PostAsJsonAsync("/api/transacoes", maisRecente, AuthTestHelper.JsonOptions)).EnsureSuccessStatusCode();

        var maisAntiga = new TransacaoCreateDto { Descricao = "Passada", Valor = 10, Tipo = TipoTransacao.Receita, Data = hoje.AddDays(-5), PessoaId = pessoaId, CategoriaId = categoriaId };
        (await Client.PostAsJsonAsync("/api/transacoes", maisAntiga, AuthTestHelper.JsonOptions)).EnsureSuccessStatusCode();

        var listaResponse = await Client.GetAsync("/api/transacoes?pagina=1&tamanhoPagina=50");
        var pagina = await listaResponse.Content.ReadFromJsonAsync<PaginacaoResultado<TransacaoResponseDto>>(AuthTestHelper.JsonOptions);

        Assert.Equal("Futura", pagina!.Itens[0].Descricao);
        Assert.Equal("Passada", pagina.Itens[1].Descricao);
    }
}
