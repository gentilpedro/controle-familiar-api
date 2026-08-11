using System.Net.Http.Json;
using ControleFamiliarAPI.Data;
using ControleFamiliarAPI.DTOs.Auth;
using ControleFamiliarAPI.DTOs.Categoria;
using ControleFamiliarAPI.Responses;
using ControleFamiliarAPI.Tests.Infrastructure;

namespace ControleFamiliarAPI.Tests.Integration;

public class CategoriasPadraoTests : IntegrationTestBase
{
    private async Task<List<CategoriaResponseDto>> ListarCategoriasAsync()
    {
        var response = await Client.GetAsync("/api/categorias");
        response.EnsureSuccessStatusCode();

        // GET /api/categorias devolve a lista crua, sem o envelope ApiResponse
        // usado pelos endpoints de auth.
        return (await response.Content
            .ReadFromJsonAsync<List<CategoriaResponseDto>>(AuthTestHelper.JsonOptions))!;
    }

    [Fact]
    public async Task FamiliaNova_JaNasceComAsCategoriasPadrao()
    {
        var auth = await AuthTestHelper.RegistrarNovaFamiliaAsync(Client);
        Client.ComToken(auth.Token);

        var categorias = await ListarCategoriasAsync();

        Assert.Equal(CategoriasPadrao.Itens.Count, categorias.Count);

        foreach (var (descricao, finalidade) in CategoriasPadrao.Itens)
        {
            var criada = categorias.SingleOrDefault(c => c.Descricao == descricao);

            Assert.NotNull(criada);
            Assert.Equal(finalidade, criada!.Finalidade);
        }
    }

    // As categorias são da família, não do sistema: têm que poder ser
    // excluídas. Se um dia virarem read-only, este teste avisa.
    [Fact]
    public async Task CategoriaPadrao_PodeSerExcluidaPelaFamilia()
    {
        var auth = await AuthTestHelper.RegistrarNovaFamiliaAsync(Client);
        Client.ComToken(auth.Token);

        var categorias = await ListarCategoriasAsync();
        var luz = categorias.Single(c => c.Descricao == "Luz");

        var response = await Client.DeleteAsync($"/api/categorias/{luz.Id}");
        response.EnsureSuccessStatusCode();

        var restantes = await ListarCategoriasAsync();
        Assert.DoesNotContain(restantes, c => c.Descricao == "Luz");
    }

    // Quem entra por convite compartilha as categorias da família que já
    // existe — não pode ganhar uma segunda cópia das padrão.
    [Fact]
    public async Task EntrarEmFamiliaExistente_NaoDuplicaAsCategorias()
    {
        var dono = await AuthTestHelper.RegistrarNovaFamiliaAsync(Client, email: $"{Guid.NewGuid():N}@teste.com");

        var convidadoDto = new RegistrarDto
        {
            Nome = "Convidado",
            Email = $"{Guid.NewGuid():N}@teste.com",
            Senha = "Senha123",
            ModoFamilia = "Entrar",
            CodigoConvite = dono.Familia.CodigoConvite
        };

        var response = await Client.PostAsJsonAsync("/api/auth/registrar", convidadoDto, AuthTestHelper.JsonOptions);
        response.EnsureSuccessStatusCode();

        var envelope = await response.Content.ReadFromJsonAsync<ApiResponse<AuthResponseDto>>(AuthTestHelper.JsonOptions);

        Client.ComToken(envelope!.Data!.Token);
        var categorias = await ListarCategoriasAsync();

        Assert.Equal(CategoriasPadrao.Itens.Count, categorias.Count);
    }
}
