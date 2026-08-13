using System.Net;
using System.Net.Http.Json;
using ControleFamiliarAPI.Data;
using ControleFamiliarAPI.DTOs.Auth;
using ControleFamiliarAPI.DTOs.FormaPagamento;
using ControleFamiliarAPI.DTOs.Pessoa;
using ControleFamiliarAPI.DTOs.Transacao;
using ControleFamiliarAPI.Models.Enums;
using ControleFamiliarAPI.Responses;
using ControleFamiliarAPI.Tests.Infrastructure;

namespace ControleFamiliarAPI.Tests.Integration;

/// <summary>
/// O catálogo de formas de pagamento: Pix/Dinheiro/Saque sem dono, visíveis
/// para todas as famílias e imutáveis para elas, ao lado das criadas por cada
/// família. Mesmas garantias de CategoriasPadraoTests.
/// </summary>
public class FormasPagamentoTests : IntegrationTestBase
{
    private async Task<List<FormaPagamentoResponseDto>> ListarAsync()
    {
        var response = await Client.GetAsync("/api/formas-pagamento");
        response.EnsureSuccessStatusCode();

        // Devolve a lista crua, sem o envelope ApiResponse — igual a /categorias.
        return (await response.Content
            .ReadFromJsonAsync<List<FormaPagamentoResponseDto>>(AuthTestHelper.JsonOptions))!;
    }

    private async Task<AuthResponseDto> NovaFamiliaAsync() =>
        await AuthTestHelper.RegistrarNovaFamiliaAsync(Client, email: $"{Guid.NewGuid():N}@teste.com");

    [Fact]
    public async Task FamiliaNova_JaEnxergaOCatalogoDoSistema()
    {
        var auth = await NovaFamiliaAsync();
        Client.ComToken(auth.Token);

        var formas = await ListarAsync();

        Assert.Equal(FormasPagamentoPadrao.Itens.Count, formas.Count);
        Assert.All(formas, f => Assert.True(f.EhDoSistema));

        foreach (var descricao in FormasPagamentoPadrao.Itens)
            Assert.Contains(formas, f => f.Descricao == descricao);
    }

    // Mesmo ponto do catálogo de categorias: não é cópia por família, é a
    // MESMA linha. Se virar cópia, os Ids divergem e este teste quebra.
    [Fact]
    public async Task FamiliasDiferentes_VeemExatamenteAsMesmasFormasDoSistema()
    {
        var primeira = await NovaFamiliaAsync();
        Client.ComToken(primeira.Token);
        var daPrimeira = await ListarAsync();

        var segunda = await NovaFamiliaAsync();
        Client.ComToken(segunda.Token);
        var daSegunda = await ListarAsync();

        Assert.Equal(
            daPrimeira.Select(f => f.Id).OrderBy(id => id),
            daSegunda.Select(f => f.Id).OrderBy(id => id));
    }

    [Fact]
    public async Task FormaDoSistema_NaoPodeSerEditadaNemExcluida()
    {
        var auth = await NovaFamiliaAsync();
        Client.ComToken(auth.Token);

        var pix = (await ListarAsync()).Single(f => f.Descricao == "Pix");

        var patch = await Client.PatchAsJsonAsync(
            $"/api/formas-pagamento/{pix.Id}",
            new FormaPagamentoUpdateDto { Descricao = "PIX renomeado" },
            AuthTestHelper.JsonOptions);
        Assert.Equal(HttpStatusCode.Forbidden, patch.StatusCode);

        var delete = await Client.DeleteAsync($"/api/formas-pagamento/{pix.Id}");
        Assert.Equal(HttpStatusCode.Forbidden, delete.StatusCode);

        // E continua lá, com o nome original, para todo mundo.
        Assert.Contains(await ListarAsync(), f => f.Descricao == "Pix");
    }

    [Fact]
    public async Task FormaCriadaPelaFamilia_PodeSerEditadaEExcluida()
    {
        var auth = await NovaFamiliaAsync();
        Client.ComToken(auth.Token);

        var criar = await Client.PostAsJsonAsync(
            "/api/formas-pagamento",
            new FormaPagamentoCreateDto { Descricao = "Cartão de crédito" },
            AuthTestHelper.JsonOptions);
        criar.EnsureSuccessStatusCode();

        var criada = (await criar.Content.ReadFromJsonAsync<FormaPagamentoResponseDto>(AuthTestHelper.JsonOptions))!;
        Assert.False(criada.EhDoSistema);

        var patch = await Client.PatchAsJsonAsync(
            $"/api/formas-pagamento/{criada.Id}",
            new FormaPagamentoUpdateDto { Descricao = "Cartão Nubank" },
            AuthTestHelper.JsonOptions);
        patch.EnsureSuccessStatusCode();

        Assert.Contains(await ListarAsync(), f => f.Descricao == "Cartão Nubank");

        var delete = await Client.DeleteAsync($"/api/formas-pagamento/{criada.Id}");
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);

        Assert.DoesNotContain(await ListarAsync(), f => f.Id == criada.Id);
    }

    [Fact]
    public async Task FormaDeUmaFamilia_NaoApareceParaOutra()
    {
        var primeira = await NovaFamiliaAsync();
        Client.ComToken(primeira.Token);

        (await Client.PostAsJsonAsync(
            "/api/formas-pagamento",
            new FormaPagamentoCreateDto { Descricao = "Vale refeição" },
            AuthTestHelper.JsonOptions)).EnsureSuccessStatusCode();

        var segunda = await NovaFamiliaAsync();
        Client.ComToken(segunda.Token);

        Assert.DoesNotContain(await ListarAsync(), f => f.Descricao == "Vale refeição");
    }

    /// <summary>
    /// A FK é Restrict: sem a checagem explícita do serviço, o SaveChanges
    /// estouraria DbUpdateException e o usuário veria 500 em vez da explicação.
    /// </summary>
    [Fact]
    public async Task FormaEmUsoPorTransacao_NaoPodeSerExcluida()
    {
        var auth = await NovaFamiliaAsync();
        Client.ComToken(auth.Token);

        var criar = await Client.PostAsJsonAsync(
            "/api/formas-pagamento",
            new FormaPagamentoCreateDto { Descricao = "Boleto" },
            AuthTestHelper.JsonOptions);
        criar.EnsureSuccessStatusCode();
        var boleto = (await criar.Content.ReadFromJsonAsync<FormaPagamentoResponseDto>(AuthTestHelper.JsonOptions))!;

        var pessoa = await Client.PostAsJsonAsync(
            "/api/pessoas",
            new PessoaCreateDto { Nome = "Adulto", Idade = 30 },
            AuthTestHelper.JsonOptions);
        pessoa.EnsureSuccessStatusCode();
        var pessoaId = (await pessoa.Content
            .ReadFromJsonAsync<ApiResponse<PessoaResponseDto>>(AuthTestHelper.JsonOptions))!.Data!.Id;

        var categorias = await (await Client.GetAsync("/api/categorias"))
            .Content.ReadFromJsonAsync<List<DTOs.Categoria.CategoriaResponseDto>>(AuthTestHelper.JsonOptions);
        var mercado = categorias!.Single(c => c.Descricao == "Mercado");

        (await Client.PostAsJsonAsync(
            "/api/transacoes",
            new TransacaoCreateDto
            {
                Descricao = "Conta de luz",
                Valor = 120,
                Tipo = TipoTransacao.Despesa,
                Data = DateOnly.FromDateTime(DateTime.UtcNow),
                PessoaId = pessoaId,
                CategoriaId = mercado.Id,
                FormaPagamentoId = boleto.Id
            },
            AuthTestHelper.JsonOptions)).EnsureSuccessStatusCode();

        var delete = await Client.DeleteAsync($"/api/formas-pagamento/{boleto.Id}");

        Assert.Equal(HttpStatusCode.BadRequest, delete.StatusCode);
        Assert.Contains(await ListarAsync(), f => f.Id == boleto.Id);
    }
}
