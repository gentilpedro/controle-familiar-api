using System.Net;
using System.Net.Http.Json;
using ControleFamiliarAPI.Data;
using ControleFamiliarAPI.DTOs.Auth;
using ControleFamiliarAPI.DTOs.Categoria;
using ControleFamiliarAPI.DTOs.Transacao;
using ControleFamiliarAPI.Models.Enums;
using ControleFamiliarAPI.Responses;
using ControleFamiliarAPI.Tests.Infrastructure;

namespace ControleFamiliarAPI.Tests.Integration;

/// <summary>
/// O catálogo de categorias do sistema: sem dono, visível para todas as
/// famílias e imutável para elas.
/// </summary>
public class CategoriasPadraoTests : IntegrationTestBase
{
    private async Task<List<CategoriaResponseDto>> ListarCategoriasAsync()
    {
        var response = await Client.GetAsync("/api/categorias");
        response.EnsureSuccessStatusCode();

        // GET /api/categorias devolve a lista crua, sem o envelope ApiResponse.
        return (await response.Content
            .ReadFromJsonAsync<List<CategoriaResponseDto>>(AuthTestHelper.JsonOptions))!;
    }

    private async Task<AuthResponseDto> NovaFamiliaAsync() =>
        await AuthTestHelper.RegistrarNovaFamiliaAsync(Client, email: $"{Guid.NewGuid():N}@teste.com");

    [Fact]
    public async Task FamiliaNova_JaEnxergaOCatalogoDoSistema()
    {
        var auth = await NovaFamiliaAsync();
        Client.ComToken(auth.Token);

        var categorias = await ListarCategoriasAsync();

        Assert.Equal(CategoriasPadrao.Itens.Count, categorias.Count);

        foreach (var (descricao, finalidade) in CategoriasPadrao.Itens)
        {
            var doCatalogo = categorias.SingleOrDefault(c => c.Descricao == descricao);

            Assert.NotNull(doCatalogo);
            Assert.Equal(finalidade, doCatalogo!.Finalidade);
        }
    }

    // O ponto central do modelo: não é uma cópia por família, é a MESMA linha.
    // Se voltar a ser cópia, os Ids divergem e este teste quebra.
    [Fact]
    public async Task FamiliasDiferentes_VeemExatamenteAsMesmasCategorias()
    {
        var primeira = await NovaFamiliaAsync();
        Client.ComToken(primeira.Token);
        var daPrimeira = await ListarCategoriasAsync();

        var segunda = await NovaFamiliaAsync();
        Client.ComToken(segunda.Token);
        var daSegunda = await ListarCategoriasAsync();

        Assert.Equal(
            daPrimeira.Select(c => c.Id).OrderBy(id => id),
            daSegunda.Select(c => c.Id).OrderBy(id => id));
    }

    [Fact]
    public async Task CategoriaDoSistema_NaoPodeSerExcluida()
    {
        var auth = await NovaFamiliaAsync();
        Client.ComToken(auth.Token);

        var luz = (await ListarCategoriasAsync()).Single(c => c.Descricao == "Luz");

        var response = await Client.DeleteAsync($"/api/categorias/{luz.Id}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        // E continua lá para todo mundo.
        Assert.Contains(await ListarCategoriasAsync(), c => c.Descricao == "Luz");
    }

    [Fact]
    public async Task CategoriaCriadaPelaFamilia_ContinuaPodendoSerExcluida()
    {
        var auth = await NovaFamiliaAsync();
        Client.ComToken(auth.Token);

        var criar = await Client.PostAsJsonAsync(
            "/api/categorias",
            new CategoriaCreateDto { Descricao = "Pet", Finalidade = FinalidadeCategoria.Despesa },
            AuthTestHelper.JsonOptions);
        criar.EnsureSuccessStatusCode();

        var pet = (await ListarCategoriasAsync()).Single(c => c.Descricao == "Pet");

        var response = await Client.DeleteAsync($"/api/categorias/{pet.Id}");
        response.EnsureSuccessStatusCode();

        Assert.DoesNotContain(await ListarCategoriasAsync(), c => c.Descricao == "Pet");
    }

    // Categoria própria continua privada: o catálogo ser compartilhado não pode
    // ter aberto o isolamento entre famílias.
    [Fact]
    public async Task CategoriaDeUmaFamilia_NaoApareceParaOutra()
    {
        var primeira = await NovaFamiliaAsync();
        Client.ComToken(primeira.Token);

        var criar = await Client.PostAsJsonAsync(
            "/api/categorias",
            new CategoriaCreateDto { Descricao = "Só Minha", Finalidade = FinalidadeCategoria.Despesa },
            AuthTestHelper.JsonOptions);
        criar.EnsureSuccessStatusCode();

        var segunda = await NovaFamiliaAsync();
        Client.ComToken(segunda.Token);

        Assert.DoesNotContain(await ListarCategoriasAsync(), c => c.Descricao == "Só Minha");
    }

    [Fact]
    public async Task Transacao_PodeUsarUmaCategoriaDoSistema()
    {
        var auth = await NovaFamiliaAsync();
        Client.ComToken(auth.Token);

        var pessoa = await Client.PostAsJsonAsync(
            "/api/pessoas",
            new DTOs.Pessoa.PessoaCreateDto { Nome = "Adulto", Idade = 30 },
            AuthTestHelper.JsonOptions);
        pessoa.EnsureSuccessStatusCode();
        var pessoaId = (await pessoa.Content
            .ReadFromJsonAsync<ApiResponse<DTOs.Pessoa.PessoaResponseDto>>(AuthTestHelper.JsonOptions))!.Data!.Id;

        var mercado = (await ListarCategoriasAsync()).Single(c => c.Descricao == "Mercado");

        var response = await Client.PostAsJsonAsync(
            "/api/transacoes",
            new TransacaoCreateDto
            {
                Descricao = "Compra do mês",
                Valor = 250,
                Tipo = TipoTransacao.Despesa,
                Data = DateOnly.FromDateTime(DateTime.UtcNow),
                PessoaId = pessoaId,
                CategoriaId = mercado.Id
            },
            AuthTestHelper.JsonOptions);

        response.EnsureSuccessStatusCode();
    }
}
