using System.Net;
using System.Net.Http.Json;
using ControleFamiliarAPI.DTOs.Categoria;
using ControleFamiliarAPI.DTOs.PainelMensal;
using ControleFamiliarAPI.DTOs.Paginacao;
using ControleFamiliarAPI.DTOs.Pessoa;
using ControleFamiliarAPI.DTOs.Transacao;
using ControleFamiliarAPI.Models.Enums;
using ControleFamiliarAPI.Responses;
using ControleFamiliarAPI.Tests.Infrastructure;

namespace ControleFamiliarAPI.Tests.Integration;

public class PainelMensalTests : IntegrationTestBase
{
    private async Task<(int PessoaId, int CategoriaId)> CriarPessoaECategoriaAsync(FinalidadeCategoria finalidadeCategoria)
    {
        var pessoaResponse = await Client.PostAsJsonAsync("/api/pessoas", new PessoaCreateDto { Nome = "Pessoa Teste", Idade = 30 }, AuthTestHelper.JsonOptions);
        var pessoa = await pessoaResponse.Content.ReadFromJsonAsync<ApiResponse<PessoaResponseDto>>(AuthTestHelper.JsonOptions);

        var categoriaResponse = await Client.PostAsJsonAsync("/api/categorias", new CategoriaCreateDto { Descricao = "Categoria Teste", Finalidade = finalidadeCategoria }, AuthTestHelper.JsonOptions);
        var categoria = await categoriaResponse.Content.ReadFromJsonAsync<CategoriaResponseDto>(AuthTestHelper.JsonOptions);

        return (pessoa!.Data!.Id, categoria!.Id);
    }

    private async Task CriarTransacaoAsync(int pessoaId, int categoriaId, TipoTransacao tipo, decimal valor, DateOnly data, bool pago)
    {
        var dto = new TransacaoCreateDto
        {
            Descricao = "Teste",
            Valor = valor,
            Tipo = tipo,
            Data = data,
            Pago = pago,
            PessoaId = pessoaId,
            CategoriaId = categoriaId
        };
        (await Client.PostAsJsonAsync("/api/transacoes", dto, AuthTestHelper.JsonOptions)).EnsureSuccessStatusCode();
    }

    private async Task<ResumoMensalDto> ObterResumoAsync(int ano, int mes)
    {
        var response = await Client.GetAsync($"/api/painel-mensal?ano={ano}&mes={mes}");
        response.EnsureSuccessStatusCode();
        var envelope = await response.Content.ReadFromJsonAsync<ApiResponse<ResumoMensalDto>>(AuthTestHelper.JsonOptions);
        return envelope!.Data!;
    }

    [Fact]
    public async Task ObterResumo_SemTransacoesNoMes_RetornaZerado()
    {
        var auth = await AuthTestHelper.RegistrarNovaFamiliaAsync(Client);
        Client.ComToken(auth.Token);

        var resumo = await ObterResumoAsync(2026, 8);

        Assert.Equal(0, resumo.TotalReceitasConfirmadas);
        Assert.Equal(0, resumo.TotalDespesasConfirmadas);
        Assert.Equal(0, resumo.Saldo);
        Assert.False(resumo.MesFechado);
    }

    [Fact]
    public async Task ObterResumo_SomaConfirmadasESeparaPendentes()
    {
        var auth = await AuthTestHelper.RegistrarNovaFamiliaAsync(Client);
        Client.ComToken(auth.Token);

        var (pessoaId, categoriaAmbas) = await CriarPessoaECategoriaAsync(FinalidadeCategoria.Ambas);

        await CriarTransacaoAsync(pessoaId, categoriaAmbas, TipoTransacao.Receita, 1000, new DateOnly(2026, 8, 10), pago: true);
        await CriarTransacaoAsync(pessoaId, categoriaAmbas, TipoTransacao.Receita, 200, new DateOnly(2026, 8, 20), pago: false);
        await CriarTransacaoAsync(pessoaId, categoriaAmbas, TipoTransacao.Despesa, 300, new DateOnly(2026, 8, 15), pago: true);
        await CriarTransacaoAsync(pessoaId, categoriaAmbas, TipoTransacao.Despesa, 50, new DateOnly(2026, 8, 25), pago: false);
        // Fora do mês — não deve entrar na soma.
        await CriarTransacaoAsync(pessoaId, categoriaAmbas, TipoTransacao.Receita, 9999, new DateOnly(2026, 9, 1), pago: true);

        var resumo = await ObterResumoAsync(2026, 8);

        Assert.Equal(1000, resumo.TotalReceitasConfirmadas);
        Assert.Equal(200, resumo.TotalReceitasPendentes);
        Assert.Equal(300, resumo.TotalDespesasConfirmadas);
        Assert.Equal(50, resumo.TotalDespesasPendentes);
        Assert.Equal(700, resumo.Saldo); // 1000 - 300, pendências não contam
    }

    [Fact]
    public async Task ObterResumo_ComMesInvalido_Retorna400()
    {
        var auth = await AuthTestHelper.RegistrarNovaFamiliaAsync(Client);
        Client.ComToken(auth.Token);

        var response = await Client.GetAsync("/api/painel-mensal?ano=2026&mes=13");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task FecharMes_ComSaldoPositivo_CriaTransacaoDeReceitaNoMesSeguinte()
    {
        var auth = await AuthTestHelper.RegistrarNovaFamiliaAsync(Client);
        Client.ComToken(auth.Token);

        var (pessoaId, categoriaAmbas) = await CriarPessoaECategoriaAsync(FinalidadeCategoria.Ambas);
        await CriarTransacaoAsync(pessoaId, categoriaAmbas, TipoTransacao.Receita, 1000, new DateOnly(2026, 8, 10), pago: true);
        await CriarTransacaoAsync(pessoaId, categoriaAmbas, TipoTransacao.Despesa, 400, new DateOnly(2026, 8, 15), pago: true);

        var fecharResponse = await Client.PostAsJsonAsync("/api/painel-mensal/fechar", new FecharMesDto { Ano = 2026, Mes = 8 }, AuthTestHelper.JsonOptions);
        fecharResponse.EnsureSuccessStatusCode();

        var lista = await (await Client.GetAsync("/api/transacoes?pagina=1&tamanhoPagina=50"))
            .Content.ReadFromJsonAsync<PaginacaoResultado<TransacaoResponseDto>>(AuthTestHelper.JsonOptions);

        var saldoGerado = lista!.Itens.Single(t => t.Categoria == "Saldo Anterior");

        Assert.Equal(TipoTransacao.Receita, saldoGerado.Tipo);
        Assert.Equal(600, saldoGerado.Valor);
        Assert.Equal(new DateOnly(2026, 9, 1), saldoGerado.Data);
        Assert.True(saldoGerado.Pago);
    }

    [Fact]
    public async Task FecharMes_ComSaldoNegativo_CriaTransacaoDeDespesaNoMesSeguinte()
    {
        var auth = await AuthTestHelper.RegistrarNovaFamiliaAsync(Client);
        Client.ComToken(auth.Token);

        var (pessoaId, categoriaAmbas) = await CriarPessoaECategoriaAsync(FinalidadeCategoria.Ambas);
        await CriarTransacaoAsync(pessoaId, categoriaAmbas, TipoTransacao.Receita, 300, new DateOnly(2026, 8, 10), pago: true);
        await CriarTransacaoAsync(pessoaId, categoriaAmbas, TipoTransacao.Despesa, 800, new DateOnly(2026, 8, 15), pago: true);

        (await Client.PostAsJsonAsync("/api/painel-mensal/fechar", new FecharMesDto { Ano = 2026, Mes = 8 }, AuthTestHelper.JsonOptions)).EnsureSuccessStatusCode();

        var lista = await (await Client.GetAsync("/api/transacoes?pagina=1&tamanhoPagina=50"))
            .Content.ReadFromJsonAsync<PaginacaoResultado<TransacaoResponseDto>>(AuthTestHelper.JsonOptions);

        var saldoGerado = lista!.Itens.Single(t => t.Categoria == "Saldo Anterior");

        Assert.Equal(TipoTransacao.Despesa, saldoGerado.Tipo);
        Assert.Equal(500, saldoGerado.Valor);
    }

    [Fact]
    public async Task FecharMes_ComSaldoZero_NaoCriaTransacao()
    {
        var auth = await AuthTestHelper.RegistrarNovaFamiliaAsync(Client);
        Client.ComToken(auth.Token);

        var (pessoaId, categoriaAmbas) = await CriarPessoaECategoriaAsync(FinalidadeCategoria.Ambas);
        await CriarTransacaoAsync(pessoaId, categoriaAmbas, TipoTransacao.Receita, 500, new DateOnly(2026, 8, 10), pago: true);
        await CriarTransacaoAsync(pessoaId, categoriaAmbas, TipoTransacao.Despesa, 500, new DateOnly(2026, 8, 15), pago: true);

        (await Client.PostAsJsonAsync("/api/painel-mensal/fechar", new FecharMesDto { Ano = 2026, Mes = 8 }, AuthTestHelper.JsonOptions)).EnsureSuccessStatusCode();

        var lista = await (await Client.GetAsync("/api/transacoes?pagina=1&tamanhoPagina=50"))
            .Content.ReadFromJsonAsync<PaginacaoResultado<TransacaoResponseDto>>(AuthTestHelper.JsonOptions);

        Assert.DoesNotContain(lista!.Itens, t => t.Categoria == "Saldo Anterior");

        var resumo = await ObterResumoAsync(2026, 8);
        Assert.True(resumo.MesFechado); // fechado mesmo sem transação gerada
    }

    [Fact]
    public async Task FecharMes_DuasVezes_Retorna400NaSegunda()
    {
        var auth = await AuthTestHelper.RegistrarNovaFamiliaAsync(Client);
        Client.ComToken(auth.Token);

        (await Client.PostAsJsonAsync("/api/painel-mensal/fechar", new FecharMesDto { Ano = 2026, Mes = 8 }, AuthTestHelper.JsonOptions)).EnsureSuccessStatusCode();

        var segundaResponse = await Client.PostAsJsonAsync("/api/painel-mensal/fechar", new FecharMesDto { Ano = 2026, Mes = 8 }, AuthTestHelper.JsonOptions);

        Assert.Equal(HttpStatusCode.BadRequest, segundaResponse.StatusCode);
    }

    [Fact]
    public async Task FecharMes_ResumoPassaAIndicarMesFechado()
    {
        var auth = await AuthTestHelper.RegistrarNovaFamiliaAsync(Client);
        Client.ComToken(auth.Token);

        var antes = await ObterResumoAsync(2026, 8);
        Assert.False(antes.MesFechado);

        (await Client.PostAsJsonAsync("/api/painel-mensal/fechar", new FecharMesDto { Ano = 2026, Mes = 8 }, AuthTestHelper.JsonOptions)).EnsureSuccessStatusCode();

        var depois = await ObterResumoAsync(2026, 8);
        Assert.True(depois.MesFechado);
        Assert.NotNull(depois.FechadoEm);
    }

    /// <summary>
    /// Dezembro fechado precisa gerar a transação em janeiro do ano
    /// seguinte, não em "mês 13" de 2026.
    /// </summary>
    [Fact]
    public async Task FecharMes_EmDezembro_GeraTransacaoEmJaneiroDoAnoSeguinte()
    {
        var auth = await AuthTestHelper.RegistrarNovaFamiliaAsync(Client);
        Client.ComToken(auth.Token);

        var (pessoaId, categoriaAmbas) = await CriarPessoaECategoriaAsync(FinalidadeCategoria.Ambas);
        await CriarTransacaoAsync(pessoaId, categoriaAmbas, TipoTransacao.Receita, 1000, new DateOnly(2026, 12, 10), pago: true);

        (await Client.PostAsJsonAsync("/api/painel-mensal/fechar", new FecharMesDto { Ano = 2026, Mes = 12 }, AuthTestHelper.JsonOptions)).EnsureSuccessStatusCode();

        var lista = await (await Client.GetAsync("/api/transacoes?pagina=1&tamanhoPagina=50"))
            .Content.ReadFromJsonAsync<PaginacaoResultado<TransacaoResponseDto>>(AuthTestHelper.JsonOptions);

        var saldoGerado = lista!.Itens.Single(t => t.Categoria == "Saldo Anterior");
        Assert.Equal(new DateOnly(2027, 1, 1), saldoGerado.Data);
    }

    /// <summary>
    /// O saldo transportado de julho vira uma transação normal em agosto —
    /// ao fechar agosto, ele já entra na soma sozinho, sem lógica especial
    /// de "saldo do saldo".
    /// </summary>
    [Fact]
    public async Task FecharMes_SaldoTransportadoContaNoFechamentoDoMesSeguinte()
    {
        var auth = await AuthTestHelper.RegistrarNovaFamiliaAsync(Client);
        Client.ComToken(auth.Token);

        var (pessoaId, categoriaAmbas) = await CriarPessoaECategoriaAsync(FinalidadeCategoria.Ambas);
        await CriarTransacaoAsync(pessoaId, categoriaAmbas, TipoTransacao.Receita, 1000, new DateOnly(2026, 7, 10), pago: true);
        await CriarTransacaoAsync(pessoaId, categoriaAmbas, TipoTransacao.Despesa, 300, new DateOnly(2026, 7, 15), pago: true);

        // Fecha julho: sobra 700, vira Receita em 01/08.
        (await Client.PostAsJsonAsync("/api/painel-mensal/fechar", new FecharMesDto { Ano = 2026, Mes = 7 }, AuthTestHelper.JsonOptions)).EnsureSuccessStatusCode();

        var resumoAgosto = await ObterResumoAsync(2026, 8);
        Assert.Equal(700, resumoAgosto.TotalReceitasConfirmadas);
        Assert.Equal(700, resumoAgosto.Saldo);
    }
}
