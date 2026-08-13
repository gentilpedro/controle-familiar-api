using System.Net;
using System.Net.Http.Json;
using ControleFamiliarAPI.DTOs.Categoria;
using ControleFamiliarAPI.DTOs.Fatura;
using ControleFamiliarAPI.DTOs.FormaPagamento;
using ControleFamiliarAPI.DTOs.Pessoa;
using ControleFamiliarAPI.DTOs.Transacao;
using ControleFamiliarAPI.Models.Enums;
using ControleFamiliarAPI.Responses;
using ControleFamiliarAPI.Tests.Infrastructure;

namespace ControleFamiliarAPI.Tests.Integration;

/// <summary>
/// Fatura de cartão: um recorte calculado das transações lançadas com aquela
/// forma de pagamento, entre um fechamento e o seguinte. Nada é gravado.
/// </summary>
public class FaturasTests : IntegrationTestBase
{
    private int _pessoaId;
    private int _categoriaId;

    private async Task PrepararFamiliaAsync()
    {
        var auth = await AuthTestHelper.RegistrarNovaFamiliaAsync(Client, email: $"{Guid.NewGuid():N}@teste.com");
        Client.ComToken(auth.Token);

        var pessoa = await Client.PostAsJsonAsync(
            "/api/pessoas", new PessoaCreateDto { Nome = "Adulto", Idade = 30 }, AuthTestHelper.JsonOptions);
        pessoa.EnsureSuccessStatusCode();
        _pessoaId = (await pessoa.Content
            .ReadFromJsonAsync<ApiResponse<PessoaResponseDto>>(AuthTestHelper.JsonOptions))!.Data!.Id;

        var categoria = await Client.PostAsJsonAsync(
            "/api/categorias",
            new CategoriaCreateDto { Descricao = "Compras", Finalidade = FinalidadeCategoria.Ambas },
            AuthTestHelper.JsonOptions);
        categoria.EnsureSuccessStatusCode();
        _categoriaId = (await categoria.Content
            .ReadFromJsonAsync<CategoriaResponseDto>(AuthTestHelper.JsonOptions))!.Id;
    }

    private async Task<FormaPagamentoResponseDto> CriarFormaAsync(
        string descricao, int? diaFechamento = null, int? diaVencimento = null, int? categoriaFaturaId = null)
    {
        var response = await Client.PostAsJsonAsync(
            "/api/formas-pagamento",
            new FormaPagamentoCreateDto
            {
                Descricao = descricao,
                DiaFechamento = diaFechamento,
                DiaVencimento = diaVencimento,
                CategoriaFaturaId = categoriaFaturaId
            },
            AuthTestHelper.JsonOptions);

        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<FormaPagamentoResponseDto>(AuthTestHelper.JsonOptions))!;
    }

    private async Task LancarAsync(string descricao, decimal valor, DateOnly data, int? formaPagamentoId, TipoTransacao tipo = TipoTransacao.Despesa)
    {
        var response = await Client.PostAsJsonAsync(
            "/api/transacoes",
            new TransacaoCreateDto
            {
                Descricao = descricao,
                Valor = valor,
                Tipo = tipo,
                Data = data,
                PessoaId = _pessoaId,
                CategoriaId = _categoriaId,
                FormaPagamentoId = formaPagamentoId
            },
            AuthTestHelper.JsonOptions);

        response.EnsureSuccessStatusCode();
    }

    private async Task<List<FaturaDto>> ListarPorVencimentoAsync(int ano, int mes)
    {
        var response = await Client.GetAsync($"/api/faturas?ano={ano}&mes={mes}");
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<List<FaturaDto>>(AuthTestHelper.JsonOptions))!;
    }

    /// <summary>
    /// O caso do dia a dia: fecha dia 28, vence dia 10 do mês seguinte. A
    /// compra do dia do fechamento entra; a do dia seguinte já é da próxima.
    /// </summary>
    [Fact]
    public async Task Fatura_AgrupaDoDiaSeguinteAoFechamentoAnteriorAteODiaDoFechamento()
    {
        await PrepararFamiliaAsync();
        var cartao = await CriarFormaAsync("Crédito Santander", diaFechamento: 28, diaVencimento: 10);

        await LancarAsync("Antes do ciclo", 10, new DateOnly(2026, 7, 28), cartao.Id);   // fatura anterior
        await LancarAsync("Primeiro dia", 100, new DateOnly(2026, 7, 29), cartao.Id);    // entra
        await LancarAsync("Meio do ciclo", 50, new DateOnly(2026, 8, 15), cartao.Id);    // entra
        await LancarAsync("Dia do fechamento", 25, new DateOnly(2026, 8, 28), cartao.Id); // entra
        await LancarAsync("Depois do fechamento", 999, new DateOnly(2026, 8, 29), cartao.Id); // próxima

        // Ciclo 29/07 a 28/08 vence em 10/09.
        var fatura = Assert.Single(await ListarPorVencimentoAsync(2026, 9));

        Assert.Equal(new DateOnly(2026, 7, 29), fatura.DataInicio);
        Assert.Equal(new DateOnly(2026, 8, 28), fatura.DataFechamento);
        Assert.Equal(new DateOnly(2026, 9, 10), fatura.DataVencimento);
        Assert.Equal(175m, fatura.Total);
        Assert.Equal(3, fatura.QuantidadeLancamentos);
        Assert.DoesNotContain(fatura.Lancamentos, l => l.Descricao == "Depois do fechamento");
    }

    /// <summary>
    /// A pergunta que motivou o recurso: Pix e débito não entram na fatura,
    /// mesmo sendo do mesmo banco — é a forma de pagamento que decide.
    /// </summary>
    [Fact]
    public async Task Fatura_IgnoraLancamentoDeOutraFormaDePagamento()
    {
        await PrepararFamiliaAsync();
        var cartao = await CriarFormaAsync("Crédito Santander", diaFechamento: 28, diaVencimento: 10);
        var debito = await CriarFormaAsync("Débito Santander");

        var formas = await (await Client.GetAsync("/api/formas-pagamento"))
            .Content.ReadFromJsonAsync<List<FormaPagamentoResponseDto>>(AuthTestHelper.JsonOptions);
        var pix = formas!.Single(f => f.Descricao == "Pix");

        await LancarAsync("No crédito", 100, new DateOnly(2026, 8, 15), cartao.Id);
        await LancarAsync("No débito", 200, new DateOnly(2026, 8, 15), debito.Id);
        await LancarAsync("No Pix", 300, new DateOnly(2026, 8, 15), pix.Id);
        await LancarAsync("Sem forma", 400, new DateOnly(2026, 8, 15), null);

        var fatura = Assert.Single(await ListarPorVencimentoAsync(2026, 9));

        Assert.Equal(100m, fatura.Total);
        Assert.Equal("No crédito", Assert.Single(fatura.Lancamentos).Descricao);
    }

    /// <summary>
    /// Cartão que fecha dia 31: em fevereiro o fechamento cai em 28, mas o
    /// ciclo anterior tem que continuar terminando em 31/01 — senão os dias
    /// 29 a 31 de janeiro sumiriam de todas as faturas.
    /// </summary>
    [Fact]
    public async Task Fatura_ComFechamentoNoDia31_NaoPerdeOsDiasDoFimDoMesAnterior()
    {
        await PrepararFamiliaAsync();
        var cartao = await CriarFormaAsync("Crédito dia 31", diaFechamento: 31, diaVencimento: 10);

        await LancarAsync("Fim de janeiro", 70, new DateOnly(2026, 1, 31), cartao.Id);  // fatura anterior
        await LancarAsync("Começo de fevereiro", 30, new DateOnly(2026, 2, 1), cartao.Id);
        await LancarAsync("Fim de fevereiro", 20, new DateOnly(2026, 2, 28), cartao.Id);

        // Ciclo 01/02 a 28/02 (fevereiro de 2026 tem 28 dias) vence em 10/03.
        var deMarco = Assert.Single(await ListarPorVencimentoAsync(2026, 3));
        Assert.Equal(new DateOnly(2026, 2, 1), deMarco.DataInicio);
        Assert.Equal(new DateOnly(2026, 2, 28), deMarco.DataFechamento);
        Assert.Equal(50m, deMarco.Total);

        // E o lançamento de 31/01 continua na fatura anterior, não sumiu.
        var deFevereiro = Assert.Single(await ListarPorVencimentoAsync(2026, 2));
        Assert.Equal(new DateOnly(2026, 1, 31), deFevereiro.DataFechamento);
        Assert.Equal(70m, deFevereiro.Total);
    }

    /// <summary>
    /// Cartão em que o vencimento vem depois do fechamento dentro do mesmo
    /// mês (fecha dia 1, vence dia 10) — a outra configuração possível.
    /// </summary>
    [Fact]
    public async Task Fatura_ComVencimentoNoMesmoMesDoFechamento_VenceNoMesDoFechamento()
    {
        await PrepararFamiliaAsync();
        var cartao = await CriarFormaAsync("Crédito adiantado", diaFechamento: 1, diaVencimento: 10);

        await LancarAsync("Compra", 80, new DateOnly(2026, 8, 15), cartao.Id);

        // Ciclo 02/08 a 01/09 vence em 10/09.
        var fatura = Assert.Single(await ListarPorVencimentoAsync(2026, 9));

        Assert.Equal(new DateOnly(2026, 9, 1), fatura.DataFechamento);
        Assert.Equal(new DateOnly(2026, 9, 10), fatura.DataVencimento);
        Assert.Equal(80m, fatura.Total);
    }

    [Fact]
    public async Task Fatura_ComFechamentoEmDezembro_ViraOAnoNoVencimento()
    {
        await PrepararFamiliaAsync();
        var cartao = await CriarFormaAsync("Crédito virada", diaFechamento: 28, diaVencimento: 10);

        await LancarAsync("Presente de Natal", 300, new DateOnly(2026, 12, 20), cartao.Id);

        var fatura = Assert.Single(await ListarPorVencimentoAsync(2027, 1));

        Assert.Equal(new DateOnly(2026, 12, 28), fatura.DataFechamento);
        Assert.Equal(new DateOnly(2027, 1, 10), fatura.DataVencimento);
        Assert.Equal(300m, fatura.Total);
    }

    /// <summary>
    /// Estorno lançado como Receita no cartão abate a fatura, como no
    /// extrato de verdade.
    /// </summary>
    [Fact]
    public async Task Fatura_ComEstornoNoCartao_AbateDoTotal()
    {
        await PrepararFamiliaAsync();
        var cartao = await CriarFormaAsync("Crédito Santander", diaFechamento: 28, diaVencimento: 10);

        await LancarAsync("Compra", 500, new DateOnly(2026, 8, 10), cartao.Id);
        await LancarAsync("Estorno", 200, new DateOnly(2026, 8, 12), cartao.Id, TipoTransacao.Receita);

        var fatura = Assert.Single(await ListarPorVencimentoAsync(2026, 9));

        Assert.Equal(300m, fatura.Total);
    }

    /// <summary>
    /// O pagamento continua sendo lançado à mão — a fatura só reconhece o
    /// que caiu na categoria vinculada dentro do mês do vencimento.
    /// </summary>
    [Fact]
    public async Task Fatura_ReconheceOPagamentoLancadoNaCategoriaVinculada()
    {
        await PrepararFamiliaAsync();

        var categoriaFatura = await Client.PostAsJsonAsync(
            "/api/categorias",
            new CategoriaCreateDto { Descricao = "Fatura Santander", Finalidade = FinalidadeCategoria.Despesa },
            AuthTestHelper.JsonOptions);
        categoriaFatura.EnsureSuccessStatusCode();
        var categoriaFaturaId = (await categoriaFatura.Content
            .ReadFromJsonAsync<CategoriaResponseDto>(AuthTestHelper.JsonOptions))!.Id;

        var cartao = await CriarFormaAsync("Crédito Santander", 28, 10, categoriaFaturaId);

        await LancarAsync("Compra", 715.92m, new DateOnly(2026, 8, 10), cartao.Id);

        var antes = Assert.Single(await ListarPorVencimentoAsync(2026, 9));
        Assert.Equal(715.92m, antes.Total);
        Assert.Equal(0m, antes.TotalPagamentosLancados);
        Assert.Equal("Fatura Santander", antes.CategoriaFatura);

        // Pagamento lançado à mão, na categoria vinculada, no mês do vencimento.
        var pagamento = await Client.PostAsJsonAsync(
            "/api/transacoes",
            new TransacaoCreateDto
            {
                Descricao = "Pagamento cartão",
                Valor = 715.92m,
                Tipo = TipoTransacao.Despesa,
                Data = new DateOnly(2026, 9, 10),
                PessoaId = _pessoaId,
                CategoriaId = categoriaFaturaId
            },
            AuthTestHelper.JsonOptions);
        pagamento.EnsureSuccessStatusCode();

        var depois = Assert.Single(await ListarPorVencimentoAsync(2026, 9));
        Assert.Equal(715.92m, depois.TotalPagamentosLancados);

        // E o pagamento não vira lançamento da fatura: ele não foi feito no
        // cartão, foi na conta.
        Assert.DoesNotContain(depois.Lancamentos, l => l.Descricao == "Pagamento cartão");
    }

    [Fact]
    public async Task Fatura_SemCategoriaVinculada_NaoTentaReconhecerPagamento()
    {
        await PrepararFamiliaAsync();
        var cartao = await CriarFormaAsync("Crédito sem categoria", diaFechamento: 28, diaVencimento: 10);

        await LancarAsync("Compra", 100, new DateOnly(2026, 8, 10), cartao.Id);

        var fatura = Assert.Single(await ListarPorVencimentoAsync(2026, 9));

        Assert.Null(fatura.TotalPagamentosLancados);
        Assert.Null(fatura.CategoriaFatura);
    }

    [Fact]
    public async Task Faturas_NaoIncluemFormaDePagamentoSemCicloConfigurado()
    {
        await PrepararFamiliaAsync();
        await CriarFormaAsync("Vale refeição");

        await LancarAsync("Almoço", 30, new DateOnly(2026, 8, 10), null);

        Assert.Empty(await ListarPorVencimentoAsync(2026, 9));
        Assert.Empty(await (await Client.GetAsync("/api/faturas/abertas"))
            .Content.ReadFromJsonAsync<List<FaturaDto>>(AuthTestHelper.JsonOptions) ?? []);
    }

    [Fact]
    public async Task FaturaAberta_EhACicloQueContemHoje()
    {
        await PrepararFamiliaAsync();
        var hoje = DateOnly.FromDateTime(DateTime.UtcNow);

        // Fecha e vence sempre no dia seguinte ao de hoje: garante que o
        // ciclo aberto é o que contém hoje, em qualquer dia que o teste rode.
        var diaSeguinte = hoje.AddDays(1).Day;
        var cartao = await CriarFormaAsync("Crédito Santander", diaSeguinte, diaSeguinte);

        await LancarAsync("Compra de hoje", 60, hoje, cartao.Id);

        var abertas = await (await Client.GetAsync("/api/faturas/abertas"))
            .Content.ReadFromJsonAsync<List<FaturaDto>>(AuthTestHelper.JsonOptions);

        var fatura = Assert.Single(abertas!);
        Assert.False(fatura.Fechada);
        Assert.Equal(60m, fatura.Total);
    }

    [Fact]
    public async Task Fatura_DeUmaFamilia_NaoApareceParaOutra()
    {
        await PrepararFamiliaAsync();
        var cartao = await CriarFormaAsync("Crédito Santander", diaFechamento: 28, diaVencimento: 10);
        await LancarAsync("Compra", 100, new DateOnly(2026, 8, 10), cartao.Id);

        await PrepararFamiliaAsync(); // outra família, outro token

        Assert.Empty(await ListarPorVencimentoAsync(2026, 9));
    }

    [Theory]
    [InlineData(2026, 13)]
    [InlineData(2026, 0)]
    [InlineData(1800, 8)]
    public async Task Faturas_ComAnoOuMesInvalido_Retorna400(int ano, int mes)
    {
        await PrepararFamiliaAsync();

        var response = await Client.GetAsync($"/api/faturas?ano={ano}&mes={mes}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task FormaPagamento_ComSoUmDosDiasDoCiclo_Retorna400()
    {
        await PrepararFamiliaAsync();

        var response = await Client.PostAsJsonAsync(
            "/api/formas-pagamento",
            new FormaPagamentoCreateDto { Descricao = "Meio cartão", DiaFechamento = 28 },
            AuthTestHelper.JsonOptions);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task FormaPagamento_ComCategoriaDeFaturaSemCiclo_Retorna400()
    {
        await PrepararFamiliaAsync();

        var response = await Client.PostAsJsonAsync(
            "/api/formas-pagamento",
            new FormaPagamentoCreateDto { Descricao = "Sem ciclo", CategoriaFaturaId = _categoriaId },
            AuthTestHelper.JsonOptions);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    /// <summary>
    /// PATCH parcial: mandar só o dia de vencimento numa forma que ainda não
    /// é cartão gravaria meio ciclo — a validação roda sobre o resultado
    /// final da mesclagem, não sobre o campo enviado.
    /// </summary>
    [Fact]
    public async Task FormaPagamento_AtualizandoSoUmDosDiasEmFormaComum_Retorna400()
    {
        await PrepararFamiliaAsync();
        var forma = await CriarFormaAsync("Cartão da loja");

        var response = await Client.PatchAsJsonAsync(
            $"/api/formas-pagamento/{forma.Id}",
            new FormaPagamentoUpdateDto { DiaVencimento = 10 },
            AuthTestHelper.JsonOptions);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task FormaPagamento_ComRemoverCartao_VoltaASerFormaComum()
    {
        await PrepararFamiliaAsync();
        var cartao = await CriarFormaAsync("Crédito Santander", diaFechamento: 28, diaVencimento: 10);
        await LancarAsync("Compra", 100, new DateOnly(2026, 8, 10), cartao.Id);

        var patch = await Client.PatchAsJsonAsync(
            $"/api/formas-pagamento/{cartao.Id}",
            new FormaPagamentoUpdateDto { RemoverCartao = true },
            AuthTestHelper.JsonOptions);
        patch.EnsureSuccessStatusCode();

        var atualizada = (await patch.Content.ReadFromJsonAsync<FormaPagamentoResponseDto>(AuthTestHelper.JsonOptions))!;
        Assert.False(atualizada.EhCartaoCredito);
        Assert.Null(atualizada.DiaFechamento);

        // Sai do ciclo de faturas, mas o lançamento continua lá.
        Assert.Empty(await ListarPorVencimentoAsync(2026, 9));
    }
}
