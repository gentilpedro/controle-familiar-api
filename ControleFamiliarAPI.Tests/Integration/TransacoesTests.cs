using System.Net;
using System.Net.Http.Json;
using ControleFamiliarAPI.DTOs.Categoria;
using ControleFamiliarAPI.DTOs.FormaPagamento;
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

    private async Task<int> CriarTransacaoAsync(int pessoaId, int categoriaId, TipoTransacao tipo = TipoTransacao.Receita, decimal valor = 1000, DateOnly? data = null)
    {
        var dto = new TransacaoCreateDto
        {
            Descricao = "Original",
            Valor = valor,
            Tipo = tipo,
            Data = data ?? DateOnly.FromDateTime(DateTime.UtcNow),
            PessoaId = pessoaId,
            CategoriaId = categoriaId
        };
        (await Client.PostAsJsonAsync("/api/transacoes", dto, AuthTestHelper.JsonOptions)).EnsureSuccessStatusCode();

        var lista = await (await Client.GetAsync("/api/transacoes?pagina=1&tamanhoPagina=50"))
            .Content.ReadFromJsonAsync<PaginacaoResultado<TransacaoResponseDto>>(AuthTestHelper.JsonOptions);

        return lista!.Itens[0].Id;
    }

    /// <summary>
    /// Regressão do bug corrigido em Pessoas (Bloco 2 daquele recurso): PATCH
    /// parcial não pode exigir todos os campos — só o que for enviado muda.
    /// </summary>
    [Fact]
    public async Task Atualizar_EnviandoSoDescricao_AtualizaSoEssaENaoRejeitaComoInvalido()
    {
        var auth = await AuthTestHelper.RegistrarNovaFamiliaAsync(Client);
        Client.ComToken(auth.Token);

        var (pessoaId, categoriaId) = await CriarPessoaECategoriaAsync(idadePessoa: 30, FinalidadeCategoria.Ambas);
        var id = await CriarTransacaoAsync(pessoaId, categoriaId, valor: 500);

        var patchResponse = await Client.PatchAsJsonAsync(
            $"/api/transacoes/{id}", new TransacaoUpdateDto { Descricao = "Salário editado" }, AuthTestHelper.JsonOptions);

        Assert.Equal(HttpStatusCode.OK, patchResponse.StatusCode);

        var lista = await (await Client.GetAsync("/api/transacoes?pagina=1&tamanhoPagina=50"))
            .Content.ReadFromJsonAsync<PaginacaoResultado<TransacaoResponseDto>>(AuthTestHelper.JsonOptions);
        var atualizada = lista!.Itens.Single(t => t.Id == id);

        Assert.Equal("Salário editado", atualizada.Descricao);
        Assert.Equal(500, atualizada.Valor); // não enviado no PATCH -> não muda
    }

    /// <summary>
    /// REGRA 2 precisa valer sobre o resultado final: editar só o Tipo, sem
    /// tocar na Categoria, ainda tem que checar contra a Categoria que a
    /// transação já tinha.
    /// </summary>
    [Fact]
    public async Task Atualizar_MudandoTipoParaIncompativelComCategoriaAtual_Retorna400()
    {
        var auth = await AuthTestHelper.RegistrarNovaFamiliaAsync(Client);
        Client.ComToken(auth.Token);

        var (pessoaId, categoriaId) = await CriarPessoaECategoriaAsync(idadePessoa: 30, FinalidadeCategoria.Receita);
        var id = await CriarTransacaoAsync(pessoaId, categoriaId, TipoTransacao.Receita);

        var patchResponse = await Client.PatchAsJsonAsync(
            $"/api/transacoes/{id}", new TransacaoUpdateDto { Tipo = TipoTransacao.Despesa }, AuthTestHelper.JsonOptions);

        Assert.Equal(HttpStatusCode.BadRequest, patchResponse.StatusCode);
    }

    [Fact]
    public async Task Atualizar_ComTransacaoInexistente_Retorna404()
    {
        var auth = await AuthTestHelper.RegistrarNovaFamiliaAsync(Client);
        Client.ComToken(auth.Token);

        var response = await Client.PatchAsJsonAsync(
            "/api/transacoes/999999", new TransacaoUpdateDto { Descricao = "Não existe" }, AuthTestHelper.JsonOptions);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Deletar_ComDadosValidos_RemoveDaListagem()
    {
        var auth = await AuthTestHelper.RegistrarNovaFamiliaAsync(Client);
        Client.ComToken(auth.Token);

        var (pessoaId, categoriaId) = await CriarPessoaECategoriaAsync(idadePessoa: 30, FinalidadeCategoria.Ambas);
        var id = await CriarTransacaoAsync(pessoaId, categoriaId);

        var deleteResponse = await Client.DeleteAsync($"/api/transacoes/{id}");
        Assert.Equal(HttpStatusCode.OK, deleteResponse.StatusCode);

        var lista = await (await Client.GetAsync("/api/transacoes?pagina=1&tamanhoPagina=50"))
            .Content.ReadFromJsonAsync<PaginacaoResultado<TransacaoResponseDto>>(AuthTestHelper.JsonOptions);

        Assert.Equal(0, lista!.TotalItens);
    }

    [Fact]
    public async Task Deletar_ComTransacaoInexistente_Retorna404()
    {
        var auth = await AuthTestHelper.RegistrarNovaFamiliaAsync(Client);
        Client.ComToken(auth.Token);

        var response = await Client.DeleteAsync("/api/transacoes/999999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private async Task<int> BuscarCategoriaSalarioIdAsync()
    {
        var categorias = await (await Client.GetAsync("/api/categorias"))
            .Content.ReadFromJsonAsync<List<CategoriaResponseDto>>(AuthTestHelper.JsonOptions);

        return categorias!.Single(c => c.Descricao == "Salário").Id;
    }

    private async Task<List<TransacaoResponseDto>> ListarTodasAsync()
    {
        var pagina = await (await Client.GetAsync("/api/transacoes?pagina=1&tamanhoPagina=200"))
            .Content.ReadFromJsonAsync<PaginacaoResultado<TransacaoResponseDto>>(AuthTestHelper.JsonOptions);
        return pagina!.Itens;
    }

    [Fact]
    public async Task CriarParcelada_ComDadosValidos_CriaTodasAsParcelasComMesmaSerie()
    {
        var auth = await AuthTestHelper.RegistrarNovaFamiliaAsync(Client);
        Client.ComToken(auth.Token);

        var (pessoaId, categoriaId) = await CriarPessoaECategoriaAsync(idadePessoa: 30, FinalidadeCategoria.Despesa);
        var primeiraParcela = DateOnly.FromDateTime(DateTime.UtcNow);

        var dto = new TransacaoParceladaCreateDto
        {
            Descricao = "Notebook",
            ValorTotal = 3000,
            NumeroParcelas = 3,
            Tipo = TipoTransacao.Despesa,
            DataPrimeiraParcela = primeiraParcela,
            PessoaId = pessoaId,
            CategoriaId = categoriaId
        };
        var response = await Client.PostAsJsonAsync("/api/transacoes/parceladas", dto, AuthTestHelper.JsonOptions);
        response.EnsureSuccessStatusCode();

        var transacoes = (await ListarTodasAsync())
            .Where(t => t.Descricao == "Notebook")
            .OrderBy(t => t.NumeroParcela)
            .ToList();

        Assert.Equal(3, transacoes.Count);
        Assert.All(transacoes, t => Assert.Equal(transacoes[0].SerieId, t.SerieId));
        Assert.NotNull(transacoes[0].SerieId);
        Assert.Equal(new int?[] { 1, 2, 3 }, transacoes.Select(t => t.NumeroParcela));
        Assert.All(transacoes, t => Assert.Equal(3, t.TotalParcelas));
        Assert.Equal(new decimal[] { 1000m, 1000m, 1000m }, transacoes.Select(t => t.Valor));
        Assert.Equal(primeiraParcela.AddMonths(2), transacoes[2].Data);
    }

    /// <summary>
    /// R$100 em 3x não divide exato (33,33 + 33,33 + 33,34) — a soma das
    /// parcelas precisa bater com o total mesmo assim, com o resíduo do
    /// arredondamento absorvido pela última.
    /// </summary>
    [Fact]
    public async Task CriarParcelada_ComValorNaoDivisivelExato_UltimaParcelaAbsorveOResiduo()
    {
        var auth = await AuthTestHelper.RegistrarNovaFamiliaAsync(Client);
        Client.ComToken(auth.Token);

        var (pessoaId, categoriaId) = await CriarPessoaECategoriaAsync(idadePessoa: 30, FinalidadeCategoria.Despesa);

        var dto = new TransacaoParceladaCreateDto
        {
            Descricao = "Bicicleta",
            ValorTotal = 100,
            NumeroParcelas = 3,
            Tipo = TipoTransacao.Despesa,
            DataPrimeiraParcela = DateOnly.FromDateTime(DateTime.UtcNow),
            PessoaId = pessoaId,
            CategoriaId = categoriaId
        };
        (await Client.PostAsJsonAsync("/api/transacoes/parceladas", dto, AuthTestHelper.JsonOptions)).EnsureSuccessStatusCode();

        var transacoes = (await ListarTodasAsync())
            .Where(t => t.Descricao == "Bicicleta")
            .OrderBy(t => t.NumeroParcela)
            .ToList();

        Assert.Equal(100m, transacoes.Sum(t => t.Valor));
        Assert.Equal(33.33m, transacoes[0].Valor);
        Assert.Equal(33.33m, transacoes[1].Valor);
        Assert.Equal(33.34m, transacoes[2].Valor);
    }

    [Fact]
    public async Task CriarParcelada_ComValorMuitoBaixoParaTantasParcelas_Retorna400()
    {
        var auth = await AuthTestHelper.RegistrarNovaFamiliaAsync(Client);
        Client.ComToken(auth.Token);

        var (pessoaId, categoriaId) = await CriarPessoaECategoriaAsync(idadePessoa: 30, FinalidadeCategoria.Despesa);

        // R$0,05 em 10x -> Round(0.005, 2) = 0.00 por parcela.
        var dto = new TransacaoParceladaCreateDto
        {
            Descricao = "Chiclete",
            ValorTotal = 0.05m,
            NumeroParcelas = 10,
            Tipo = TipoTransacao.Despesa,
            DataPrimeiraParcela = DateOnly.FromDateTime(DateTime.UtcNow),
            PessoaId = pessoaId,
            CategoriaId = categoriaId
        };
        var response = await Client.PostAsJsonAsync("/api/transacoes/parceladas", dto, AuthTestHelper.JsonOptions);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CriarParcelada_ComCategoriaIncompativel_Retorna400()
    {
        var auth = await AuthTestHelper.RegistrarNovaFamiliaAsync(Client);
        Client.ComToken(auth.Token);

        var (pessoaId, categoriaId) = await CriarPessoaECategoriaAsync(idadePessoa: 30, FinalidadeCategoria.Receita);

        var dto = new TransacaoParceladaCreateDto
        {
            Descricao = "Notebook",
            ValorTotal = 3000,
            NumeroParcelas = 3,
            Tipo = TipoTransacao.Despesa,
            DataPrimeiraParcela = DateOnly.FromDateTime(DateTime.UtcNow),
            PessoaId = pessoaId,
            CategoriaId = categoriaId
        };
        var response = await Client.PostAsJsonAsync("/api/transacoes/parceladas", dto, AuthTestHelper.JsonOptions);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    /// <summary>
    /// Editar a parcela 2 de 3 com AplicarAFuturas propaga só pra parcela 3
    /// (NumeroParcela >= 2) — a parcela 1 (já passada) fica intocada.
    /// </summary>
    [Fact]
    public async Task Atualizar_ComAplicarAFuturas_PropagaSoParaNumeroParcelaMaiorOuIgual()
    {
        var auth = await AuthTestHelper.RegistrarNovaFamiliaAsync(Client);
        Client.ComToken(auth.Token);

        var (pessoaId, categoriaId) = await CriarPessoaECategoriaAsync(idadePessoa: 30, FinalidadeCategoria.Despesa);
        var primeiraParcela = DateOnly.FromDateTime(DateTime.UtcNow);

        var dto = new TransacaoParceladaCreateDto
        {
            Descricao = "Assinatura",
            ValorTotal = 300,
            NumeroParcelas = 3,
            Tipo = TipoTransacao.Despesa,
            DataPrimeiraParcela = primeiraParcela,
            PessoaId = pessoaId,
            CategoriaId = categoriaId
        };
        (await Client.PostAsJsonAsync("/api/transacoes/parceladas", dto, AuthTestHelper.JsonOptions)).EnsureSuccessStatusCode();

        var parcelas = (await ListarTodasAsync()).Where(t => t.Descricao == "Assinatura").OrderBy(t => t.NumeroParcela).ToList();
        var parcela2 = parcelas[1];

        var patchResponse = await Client.PatchAsJsonAsync(
            $"/api/transacoes/{parcela2.Id}",
            new TransacaoUpdateDto { Descricao = "Assinatura reajustada", AplicarAFuturas = true },
            AuthTestHelper.JsonOptions);
        patchResponse.EnsureSuccessStatusCode();

        var atualizadas = (await ListarTodasAsync()).Where(t => t.SerieId == parcela2.SerieId).OrderBy(t => t.NumeroParcela).ToList();

        Assert.Equal("Assinatura", atualizadas[0].Descricao); // parcela 1, não afetada
        Assert.Equal("Assinatura reajustada", atualizadas[1].Descricao);
        Assert.Equal("Assinatura reajustada", atualizadas[2].Descricao);
    }

    /// <summary>
    /// Data nunca propaga, mesmo com AplicarAFuturas — mudar a data da
    /// parcela editada não pode arrastar o espaçamento das seguintes.
    /// </summary>
    [Fact]
    public async Task Atualizar_ComAplicarAFuturas_NuncaPropagaData()
    {
        var auth = await AuthTestHelper.RegistrarNovaFamiliaAsync(Client);
        Client.ComToken(auth.Token);

        var (pessoaId, categoriaId) = await CriarPessoaECategoriaAsync(idadePessoa: 30, FinalidadeCategoria.Despesa);
        var primeiraParcela = DateOnly.FromDateTime(DateTime.UtcNow);

        var dto = new TransacaoParceladaCreateDto
        {
            Descricao = "Curso",
            ValorTotal = 200,
            NumeroParcelas = 2,
            Tipo = TipoTransacao.Despesa,
            DataPrimeiraParcela = primeiraParcela,
            PessoaId = pessoaId,
            CategoriaId = categoriaId
        };
        (await Client.PostAsJsonAsync("/api/transacoes/parceladas", dto, AuthTestHelper.JsonOptions)).EnsureSuccessStatusCode();

        var parcelas = (await ListarTodasAsync()).Where(t => t.Descricao == "Curso").OrderBy(t => t.NumeroParcela).ToList();
        var parcela1 = parcelas[0];
        var dataOriginalParcela2 = parcelas[1].Data;
        var novaData = primeiraParcela.AddDays(3);

        (await Client.PatchAsJsonAsync(
            $"/api/transacoes/{parcela1.Id}",
            new TransacaoUpdateDto { Data = novaData, AplicarAFuturas = true },
            AuthTestHelper.JsonOptions)).EnsureSuccessStatusCode();

        var atualizadas = (await ListarTodasAsync()).Where(t => t.SerieId == parcela1.SerieId).OrderBy(t => t.NumeroParcela).ToList();

        Assert.Equal(novaData, atualizadas[0].Data);
        Assert.Equal(dataOriginalParcela2, atualizadas[1].Data); // intocada
    }

    [Fact]
    public async Task Deletar_ComExcluirFuturas_RemoveSoAsSeguintes()
    {
        var auth = await AuthTestHelper.RegistrarNovaFamiliaAsync(Client);
        Client.ComToken(auth.Token);

        var (pessoaId, categoriaId) = await CriarPessoaECategoriaAsync(idadePessoa: 30, FinalidadeCategoria.Despesa);

        var dto = new TransacaoParceladaCreateDto
        {
            Descricao = "Cancelado",
            ValorTotal = 400,
            NumeroParcelas = 4,
            Tipo = TipoTransacao.Despesa,
            DataPrimeiraParcela = DateOnly.FromDateTime(DateTime.UtcNow),
            PessoaId = pessoaId,
            CategoriaId = categoriaId
        };
        (await Client.PostAsJsonAsync("/api/transacoes/parceladas", dto, AuthTestHelper.JsonOptions)).EnsureSuccessStatusCode();

        var parcelas = (await ListarTodasAsync()).Where(t => t.Descricao == "Cancelado").OrderBy(t => t.NumeroParcela).ToList();
        var parcela3 = parcelas[2];

        var deleteResponse = await Client.DeleteAsync($"/api/transacoes/{parcela3.Id}?excluirFuturas=true");
        deleteResponse.EnsureSuccessStatusCode();

        var restantes = (await ListarTodasAsync()).Where(t => t.SerieId == parcela3.SerieId).ToList();

        Assert.Equal(2, restantes.Count); // só as parcelas 1 e 2 sobram
        Assert.All(restantes, t => Assert.True(t.NumeroParcela < 3));
    }

    [Fact]
    public async Task CriarRecorrenciaPercentual_ComCategoriaSalario_CriaOcorrenciasComMesmaSerie()
    {
        var auth = await AuthTestHelper.RegistrarNovaFamiliaAsync(Client);
        Client.ComToken(auth.Token);

        var (pessoaId, _) = await CriarPessoaECategoriaAsync(idadePessoa: 30, FinalidadeCategoria.Ambas);
        var salarioId = await BuscarCategoriaSalarioIdAsync();

        var dto = new TransacaoRecorrenciaPercentualCreateDto
        {
            Descricao = "Salário quinzenal",
            ValorTotal = 1000,
            MesReferencia = new DateOnly(2026, 8, 1),
            Ocorrencias =
            [
                new OcorrenciaPercentualDto { Dia = 15, Percentual = 35 },
                new OcorrenciaPercentualDto { Dia = 30, Percentual = 65 }
            ],
            PessoaId = pessoaId,
            CategoriaId = salarioId
        };
        var response = await Client.PostAsJsonAsync("/api/transacoes/recorrencia-percentual", dto, AuthTestHelper.JsonOptions);
        response.EnsureSuccessStatusCode();

        var ocorrencias = (await ListarTodasAsync())
            .Where(t => t.Descricao == "Salário quinzenal")
            .OrderBy(t => t.NumeroParcela)
            .ToList();

        Assert.Equal(2, ocorrencias.Count);
        Assert.Equal(ocorrencias[0].SerieId, ocorrencias[1].SerieId);
        Assert.NotNull(ocorrencias[0].SerieId);
        Assert.All(ocorrencias, o => Assert.Equal(TipoTransacao.Receita, o.Tipo));
        Assert.Equal(350m, ocorrencias[0].Valor);
        Assert.Equal(new DateOnly(2026, 8, 15), ocorrencias[0].Data);
        Assert.Equal(650m, ocorrencias[1].Valor);
        Assert.Equal(new DateOnly(2026, 8, 30), ocorrencias[1].Data);
    }

    /// <summary>
    /// Percentuais não precisam somar 100 — adiantamento e saldo podem vir
    /// de bases diferentes. 35% + 75% = 110%, e isso é esperado.
    /// </summary>
    [Fact]
    public async Task CriarRecorrenciaPercentual_ComPercentuaisQueNaoSomam100_FuncionaMesmoAssim()
    {
        var auth = await AuthTestHelper.RegistrarNovaFamiliaAsync(Client);
        Client.ComToken(auth.Token);

        var (pessoaId, _) = await CriarPessoaECategoriaAsync(idadePessoa: 30, FinalidadeCategoria.Ambas);
        var salarioId = await BuscarCategoriaSalarioIdAsync();

        var dto = new TransacaoRecorrenciaPercentualCreateDto
        {
            Descricao = "Salário com adiantamento",
            ValorTotal = 1000,
            MesReferencia = new DateOnly(2026, 9, 1),
            Ocorrencias =
            [
                new OcorrenciaPercentualDto { Dia = 15, Percentual = 35 },
                new OcorrenciaPercentualDto { Dia = 30, Percentual = 75 }
            ],
            PessoaId = pessoaId,
            CategoriaId = salarioId
        };
        var response = await Client.PostAsJsonAsync("/api/transacoes/recorrencia-percentual", dto, AuthTestHelper.JsonOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task CriarRecorrenciaPercentual_ComCategoriaQueNaoAceitaDivisaoPercentual_Retorna400()
    {
        var auth = await AuthTestHelper.RegistrarNovaFamiliaAsync(Client);
        Client.ComToken(auth.Token);

        var (pessoaId, categoriaId) = await CriarPessoaECategoriaAsync(idadePessoa: 30, FinalidadeCategoria.Receita);

        var dto = new TransacaoRecorrenciaPercentualCreateDto
        {
            Descricao = "Freela",
            ValorTotal = 500,
            MesReferencia = new DateOnly(2026, 8, 1),
            Ocorrencias =
            [
                new OcorrenciaPercentualDto { Dia = 10, Percentual = 50 },
                new OcorrenciaPercentualDto { Dia = 20, Percentual = 50 }
            ],
            PessoaId = pessoaId,
            CategoriaId = categoriaId
        };
        var response = await Client.PostAsJsonAsync("/api/transacoes/recorrencia-percentual", dto, AuthTestHelper.JsonOptions);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CriarRecorrenciaPercentual_ComUmaSoOcorrencia_Retorna400()
    {
        var auth = await AuthTestHelper.RegistrarNovaFamiliaAsync(Client);
        Client.ComToken(auth.Token);

        var (pessoaId, _) = await CriarPessoaECategoriaAsync(idadePessoa: 30, FinalidadeCategoria.Ambas);
        var salarioId = await BuscarCategoriaSalarioIdAsync();

        var dto = new TransacaoRecorrenciaPercentualCreateDto
        {
            Descricao = "Salário integral",
            ValorTotal = 1000,
            MesReferencia = new DateOnly(2026, 8, 1),
            Ocorrencias = [new OcorrenciaPercentualDto { Dia = 30, Percentual = 100 }],
            PessoaId = pessoaId,
            CategoriaId = salarioId
        };
        var response = await Client.PostAsJsonAsync("/api/transacoes/recorrencia-percentual", dto, AuthTestHelper.JsonOptions);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    /// <summary>
    /// Dia 31 em fevereiro não existe — cai no último dia do mês, mesma
    /// filosofia do AddMonths no parcelamento.
    /// </summary>
    [Fact]
    public async Task CriarRecorrenciaPercentual_ComDiaQueNaoExisteNoMes_CaiNoUltimoDiaDoMes()
    {
        var auth = await AuthTestHelper.RegistrarNovaFamiliaAsync(Client);
        Client.ComToken(auth.Token);

        var (pessoaId, _) = await CriarPessoaECategoriaAsync(idadePessoa: 30, FinalidadeCategoria.Ambas);
        var salarioId = await BuscarCategoriaSalarioIdAsync();

        var dto = new TransacaoRecorrenciaPercentualCreateDto
        {
            Descricao = "Salário fevereiro",
            ValorTotal = 1000,
            MesReferencia = new DateOnly(2026, 2, 1), // 2026 não é bissexto -> fevereiro tem 28 dias
            Ocorrencias =
            [
                new OcorrenciaPercentualDto { Dia = 15, Percentual = 35 },
                new OcorrenciaPercentualDto { Dia = 31, Percentual = 65 }
            ],
            PessoaId = pessoaId,
            CategoriaId = salarioId
        };
        (await Client.PostAsJsonAsync("/api/transacoes/recorrencia-percentual", dto, AuthTestHelper.JsonOptions)).EnsureSuccessStatusCode();

        var ocorrencias = (await ListarTodasAsync())
            .Where(t => t.Descricao == "Salário fevereiro")
            .OrderBy(t => t.NumeroParcela)
            .ToList();

        Assert.Equal(new DateOnly(2026, 2, 28), ocorrencias[1].Data);
    }

    [Fact]
    public async Task Criar_ComPagoOmitido_NasceComoPago()
    {
        var auth = await AuthTestHelper.RegistrarNovaFamiliaAsync(Client);
        Client.ComToken(auth.Token);

        var (pessoaId, categoriaId) = await CriarPessoaECategoriaAsync(idadePessoa: 30, FinalidadeCategoria.Ambas);
        var id = await CriarTransacaoAsync(pessoaId, categoriaId);

        var transacao = (await ListarTodasAsync()).Single(t => t.Id == id);

        Assert.True(transacao.Pago);
    }

    [Fact]
    public async Task Criar_ComPagoFalse_NasceComoPendente()
    {
        var auth = await AuthTestHelper.RegistrarNovaFamiliaAsync(Client);
        Client.ComToken(auth.Token);

        var (pessoaId, categoriaId) = await CriarPessoaECategoriaAsync(idadePessoa: 30, FinalidadeCategoria.Ambas);

        var dto = new TransacaoCreateDto
        {
            Descricao = "Conta a pagar",
            Valor = 100,
            Tipo = TipoTransacao.Despesa,
            Data = DateOnly.FromDateTime(DateTime.UtcNow),
            Pago = false,
            PessoaId = pessoaId,
            CategoriaId = categoriaId
        };
        (await Client.PostAsJsonAsync("/api/transacoes", dto, AuthTestHelper.JsonOptions)).EnsureSuccessStatusCode();

        var transacao = (await ListarTodasAsync()).Single(t => t.Descricao == "Conta a pagar");

        Assert.False(transacao.Pago);
    }

    /// <summary>
    /// Parcela futura é uma obrigação até o usuário confirmar — mesmo a
    /// primeira, ainda que criada hoje.
    /// </summary>
    [Fact]
    public async Task CriarParcelada_TodasAsParcelasNascemComoPendentes()
    {
        var auth = await AuthTestHelper.RegistrarNovaFamiliaAsync(Client);
        Client.ComToken(auth.Token);

        var (pessoaId, categoriaId) = await CriarPessoaECategoriaAsync(idadePessoa: 30, FinalidadeCategoria.Despesa);

        var dto = new TransacaoParceladaCreateDto
        {
            Descricao = "Geladeira",
            ValorTotal = 900,
            NumeroParcelas = 3,
            Tipo = TipoTransacao.Despesa,
            DataPrimeiraParcela = DateOnly.FromDateTime(DateTime.UtcNow),
            PessoaId = pessoaId,
            CategoriaId = categoriaId
        };
        (await Client.PostAsJsonAsync("/api/transacoes/parceladas", dto, AuthTestHelper.JsonOptions)).EnsureSuccessStatusCode();

        var parcelas = (await ListarTodasAsync()).Where(t => t.Descricao == "Geladeira").ToList();

        Assert.Equal(3, parcelas.Count);
        Assert.All(parcelas, p => Assert.False(p.Pago));
    }

    [Fact]
    public async Task MarcarPago_AlternaOStatus()
    {
        var auth = await AuthTestHelper.RegistrarNovaFamiliaAsync(Client);
        Client.ComToken(auth.Token);

        var (pessoaId, categoriaId) = await CriarPessoaECategoriaAsync(idadePessoa: 30, FinalidadeCategoria.Ambas);
        var id = await CriarTransacaoAsync(pessoaId, categoriaId);

        var response = await Client.PatchAsJsonAsync(
            $"/api/transacoes/{id}/pago", new TransacaoPagoUpdateDto { Pago = false }, AuthTestHelper.JsonOptions);
        response.EnsureSuccessStatusCode();

        var transacao = (await ListarTodasAsync()).Single(t => t.Id == id);
        Assert.False(transacao.Pago);

        (await Client.PatchAsJsonAsync(
            $"/api/transacoes/{id}/pago", new TransacaoPagoUpdateDto { Pago = true }, AuthTestHelper.JsonOptions)).EnsureSuccessStatusCode();

        transacao = (await ListarTodasAsync()).Single(t => t.Id == id);
        Assert.True(transacao.Pago);
    }

    [Fact]
    public async Task MarcarPago_ComTransacaoInexistente_Retorna404()
    {
        var auth = await AuthTestHelper.RegistrarNovaFamiliaAsync(Client);
        Client.ComToken(auth.Token);

        var response = await Client.PatchAsJsonAsync(
            "/api/transacoes/999999/pago", new TransacaoPagoUpdateDto { Pago = true }, AuthTestHelper.JsonOptions);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private async Task<int> BuscarFormaPagamentoIdAsync(string descricao)
    {
        var formas = await (await Client.GetAsync("/api/formas-pagamento"))
            .Content.ReadFromJsonAsync<List<FormaPagamentoResponseDto>>(AuthTestHelper.JsonOptions);

        return formas!.Single(f => f.Descricao == descricao).Id;
    }

    [Fact]
    public async Task Criar_ComFormaPagamentoDoSistema_GravaEDevolveNaListagem()
    {
        var auth = await AuthTestHelper.RegistrarNovaFamiliaAsync(Client);
        Client.ComToken(auth.Token);

        var (pessoaId, categoriaId) = await CriarPessoaECategoriaAsync(idadePessoa: 30, FinalidadeCategoria.Ambas);
        var pixId = await BuscarFormaPagamentoIdAsync("Pix");

        var dto = new TransacaoCreateDto
        {
            Descricao = "Pix recebido",
            Valor = 200,
            Tipo = TipoTransacao.Receita,
            Data = DateOnly.FromDateTime(DateTime.UtcNow),
            PessoaId = pessoaId,
            CategoriaId = categoriaId,
            FormaPagamentoId = pixId
        };
        (await Client.PostAsJsonAsync("/api/transacoes", dto, AuthTestHelper.JsonOptions)).EnsureSuccessStatusCode();

        var transacao = (await ListarTodasAsync()).Single(t => t.Descricao == "Pix recebido");

        Assert.Equal(pixId, transacao.FormaPagamentoId);
        Assert.Equal("Pix", transacao.FormaPagamento);
    }

    /// <summary>
    /// O campo é opcional — transação sem forma de pagamento continua válida
    /// (é o caso de todas as que existiam antes do campo).
    /// </summary>
    [Fact]
    public async Task Criar_SemFormaPagamento_ContinuaValidaEDevolveNulo()
    {
        var auth = await AuthTestHelper.RegistrarNovaFamiliaAsync(Client);
        Client.ComToken(auth.Token);

        var (pessoaId, categoriaId) = await CriarPessoaECategoriaAsync(idadePessoa: 30, FinalidadeCategoria.Ambas);
        var id = await CriarTransacaoAsync(pessoaId, categoriaId);

        var transacao = (await ListarTodasAsync()).Single(t => t.Id == id);

        Assert.Null(transacao.FormaPagamentoId);
        Assert.Null(transacao.FormaPagamento);
    }

    [Fact]
    public async Task Criar_ComFormaPagamentoInexistente_Retorna404()
    {
        var auth = await AuthTestHelper.RegistrarNovaFamiliaAsync(Client);
        Client.ComToken(auth.Token);

        var (pessoaId, categoriaId) = await CriarPessoaECategoriaAsync(idadePessoa: 30, FinalidadeCategoria.Ambas);

        var dto = new TransacaoCreateDto
        {
            Descricao = "Forma fantasma",
            Valor = 10,
            Tipo = TipoTransacao.Despesa,
            Data = DateOnly.FromDateTime(DateTime.UtcNow),
            PessoaId = pessoaId,
            CategoriaId = categoriaId,
            FormaPagamentoId = 999999
        };
        var response = await Client.PostAsJsonAsync("/api/transacoes", dto, AuthTestHelper.JsonOptions);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    /// <summary>
    /// Num PATCH parcial, campo ausente e campo null chegam iguais — sem a
    /// flag RemoverFormaPagamento não haveria como desfazer a escolha, só
    /// trocá-la por outra.
    /// </summary>
    [Fact]
    public async Task Atualizar_ComRemoverFormaPagamento_LimpaOCampo()
    {
        var auth = await AuthTestHelper.RegistrarNovaFamiliaAsync(Client);
        Client.ComToken(auth.Token);

        var (pessoaId, categoriaId) = await CriarPessoaECategoriaAsync(idadePessoa: 30, FinalidadeCategoria.Ambas);
        var dinheiroId = await BuscarFormaPagamentoIdAsync("Dinheiro");

        var dto = new TransacaoCreateDto
        {
            Descricao = "Compra",
            Valor = 50,
            Tipo = TipoTransacao.Despesa,
            Data = DateOnly.FromDateTime(DateTime.UtcNow),
            PessoaId = pessoaId,
            CategoriaId = categoriaId,
            FormaPagamentoId = dinheiroId
        };
        (await Client.PostAsJsonAsync("/api/transacoes", dto, AuthTestHelper.JsonOptions)).EnsureSuccessStatusCode();

        var id = (await ListarTodasAsync()).Single(t => t.Descricao == "Compra").Id;

        // Só a descrição: a forma de pagamento não pode se perder no caminho.
        (await Client.PatchAsJsonAsync(
            $"/api/transacoes/{id}",
            new TransacaoUpdateDto { Descricao = "Compra editada" },
            AuthTestHelper.JsonOptions)).EnsureSuccessStatusCode();

        Assert.Equal(dinheiroId, (await ListarTodasAsync()).Single(t => t.Id == id).FormaPagamentoId);

        (await Client.PatchAsJsonAsync(
            $"/api/transacoes/{id}",
            new TransacaoUpdateDto { RemoverFormaPagamento = true },
            AuthTestHelper.JsonOptions)).EnsureSuccessStatusCode();

        var semForma = (await ListarTodasAsync()).Single(t => t.Id == id);
        Assert.Null(semForma.FormaPagamentoId);
        Assert.Null(semForma.FormaPagamento);
    }

    [Fact]
    public async Task Listar_ComFiltroDeMes_TrazSoAsTransacoesDaquelePeriodo()
    {
        var auth = await AuthTestHelper.RegistrarNovaFamiliaAsync(Client);
        Client.ComToken(auth.Token);

        var (pessoaId, categoriaId) = await CriarPessoaECategoriaAsync(idadePessoa: 30, FinalidadeCategoria.Ambas);

        // Primeiro e último dia do mês filtrado entram; um dia antes e um dia
        // depois ficam de fora — é a borda do intervalo que interessa aqui.
        await CriarTransacaoDatadaAsync(pessoaId, categoriaId, "Primeiro dia", new DateOnly(2026, 8, 1));
        await CriarTransacaoDatadaAsync(pessoaId, categoriaId, "Último dia", new DateOnly(2026, 8, 31));
        await CriarTransacaoDatadaAsync(pessoaId, categoriaId, "Mês anterior", new DateOnly(2026, 7, 31));
        await CriarTransacaoDatadaAsync(pessoaId, categoriaId, "Mês seguinte", new DateOnly(2026, 9, 1));

        var pagina = await (await Client.GetAsync("/api/transacoes?pagina=1&tamanhoPagina=50&ano=2026&mes=8"))
            .Content.ReadFromJsonAsync<PaginacaoResultado<TransacaoResponseDto>>(AuthTestHelper.JsonOptions);

        Assert.Equal(2, pagina!.TotalItens);
        Assert.Equal(
            new[] { "Primeiro dia", "Último dia" },
            pagina.Itens.Select(t => t.Descricao).OrderBy(d => d));
    }

    [Fact]
    public async Task Listar_ComFiltroDeMesEmDezembro_NaoVazaParaJaneiroSeguinte()
    {
        var auth = await AuthTestHelper.RegistrarNovaFamiliaAsync(Client);
        Client.ComToken(auth.Token);

        var (pessoaId, categoriaId) = await CriarPessoaECategoriaAsync(idadePessoa: 30, FinalidadeCategoria.Ambas);

        await CriarTransacaoDatadaAsync(pessoaId, categoriaId, "Dezembro", new DateOnly(2026, 12, 20));
        await CriarTransacaoDatadaAsync(pessoaId, categoriaId, "Janeiro", new DateOnly(2027, 1, 5));

        var pagina = await (await Client.GetAsync("/api/transacoes?pagina=1&tamanhoPagina=50&ano=2026&mes=12"))
            .Content.ReadFromJsonAsync<PaginacaoResultado<TransacaoResponseDto>>(AuthTestHelper.JsonOptions);

        Assert.Equal(1, pagina!.TotalItens);
        Assert.Equal("Dezembro", pagina.Itens[0].Descricao);
    }

    [Fact]
    public async Task Listar_SemFiltro_ContinuaTrazendoOHistoricoInteiro()
    {
        var auth = await AuthTestHelper.RegistrarNovaFamiliaAsync(Client);
        Client.ComToken(auth.Token);

        var (pessoaId, categoriaId) = await CriarPessoaECategoriaAsync(idadePessoa: 30, FinalidadeCategoria.Ambas);

        await CriarTransacaoDatadaAsync(pessoaId, categoriaId, "Agosto", new DateOnly(2026, 8, 10));
        await CriarTransacaoDatadaAsync(pessoaId, categoriaId, "Setembro", new DateOnly(2026, 9, 10));

        var pagina = await (await Client.GetAsync("/api/transacoes?pagina=1&tamanhoPagina=50"))
            .Content.ReadFromJsonAsync<PaginacaoResultado<TransacaoResponseDto>>(AuthTestHelper.JsonOptions);

        Assert.Equal(2, pagina!.TotalItens);
    }

    [Theory]
    [InlineData("ano=2026")]
    [InlineData("mes=8")]
    [InlineData("ano=2026&mes=13")]
    [InlineData("ano=2026&mes=0")]
    public async Task Listar_ComPeriodoIncompletoOuInvalido_Retorna400(string queryString)
    {
        var auth = await AuthTestHelper.RegistrarNovaFamiliaAsync(Client);
        Client.ComToken(auth.Token);

        var response = await Client.GetAsync($"/api/transacoes?pagina=1&tamanhoPagina=50&{queryString}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private async Task CriarTransacaoDatadaAsync(int pessoaId, int categoriaId, string descricao, DateOnly data)
    {
        var dto = new TransacaoCreateDto
        {
            Descricao = descricao,
            Valor = 100,
            Tipo = TipoTransacao.Despesa,
            Data = data,
            PessoaId = pessoaId,
            CategoriaId = categoriaId
        };
        (await Client.PostAsJsonAsync("/api/transacoes", dto, AuthTestHelper.JsonOptions)).EnsureSuccessStatusCode();
    }

    /// <summary>
    /// Igual à Data: Pago é por ocorrência, AplicarAFuturas nunca deveria
    /// marcar/desmarcar as outras parcelas junto.
    /// </summary>
    [Fact]
    public async Task Atualizar_ComAplicarAFuturas_NuncaPropagaPago()
    {
        var auth = await AuthTestHelper.RegistrarNovaFamiliaAsync(Client);
        Client.ComToken(auth.Token);

        var (pessoaId, categoriaId) = await CriarPessoaECategoriaAsync(idadePessoa: 30, FinalidadeCategoria.Despesa);

        var dto = new TransacaoParceladaCreateDto
        {
            Descricao = "Sofá",
            ValorTotal = 600,
            NumeroParcelas = 2,
            Tipo = TipoTransacao.Despesa,
            DataPrimeiraParcela = DateOnly.FromDateTime(DateTime.UtcNow),
            PessoaId = pessoaId,
            CategoriaId = categoriaId
        };
        (await Client.PostAsJsonAsync("/api/transacoes/parceladas", dto, AuthTestHelper.JsonOptions)).EnsureSuccessStatusCode();

        var parcelas = (await ListarTodasAsync()).Where(t => t.Descricao == "Sofá").OrderBy(t => t.NumeroParcela).ToList();
        var parcela1 = parcelas[0];

        (await Client.PatchAsJsonAsync(
            $"/api/transacoes/{parcela1.Id}",
            new TransacaoUpdateDto { Pago = true, AplicarAFuturas = true },
            AuthTestHelper.JsonOptions)).EnsureSuccessStatusCode();

        var atualizadas = (await ListarTodasAsync()).Where(t => t.SerieId == parcela1.SerieId).OrderBy(t => t.NumeroParcela).ToList();

        Assert.True(atualizadas[0].Pago);
        Assert.False(atualizadas[1].Pago); // intocada
    }
}
