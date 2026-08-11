using System.Net;
using System.Text;
using System.Net.Http.Json;
using ControleFamiliarAPI.Data;
using ControleFamiliarAPI.DTOs.Auth;
using ControleFamiliarAPI.DTOs.Categoria;
using ControleFamiliarAPI.DTOs.Pessoa;
using ControleFamiliarAPI.Models;
using ControleFamiliarAPI.Models.Enums;
using ControleFamiliarAPI.Responses;
using ControleFamiliarAPI.Tests.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ControleFamiliarAPI.Tests.Integration;

public class AuthTests : IntegrationTestBase
{
    [Fact]
    public async Task Registrar_ComModoFamiliaNova_CriaContaERetornaToken()
    {
        var auth = await AuthTestHelper.RegistrarNovaFamiliaAsync(Client);

        Assert.False(string.IsNullOrWhiteSpace(auth.Token));
        Assert.True(auth.Usuario.EhAdministrador);
        Assert.NotEmpty(auth.Familia.CodigoConvite);
    }

    // JSON cru em vez do DTO: agora que ModoFamilia é enum, um valor inválido
    // não é representável em C# — mas continua sendo o que um cliente HTTP
    // qualquer pode mandar, que é justamente o que este teste protege.
    [Fact]
    public async Task Registrar_ComModoFamiliaInvalido_Retorna400()
    {
        var json = $$"""
        {
          "nome": "Usuário Teste",
          "email": "{{Guid.NewGuid():N}}@teste.com",
          "senha": "Senha123",
          "modoFamilia": "ModoQueNaoExiste"
        }
        """;

        var conteudo = new StringContent(json, Encoding.UTF8, "application/json");
        var response = await Client.PostAsync("/api/auth/registrar", conteudo);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // Omitir o campo tem que ser 400 também: se ModoFamilia fosse um enum
    // não-anulável, a ausência cairia no valor 0 e o cadastro criaria uma
    // família nova em silêncio, sem o cliente ter pedido isso.
    [Fact]
    public async Task Registrar_SemModoFamilia_Retorna400()
    {
        var json = $$"""
        {
          "nome": "Usuário Teste",
          "email": "{{Guid.NewGuid():N}}@teste.com",
          "senha": "Senha123"
        }
        """;

        var conteudo = new StringContent(json, Encoding.UTF8, "application/json");
        var response = await Client.PostAsync("/api/auth/registrar", conteudo);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Login_ComSenhaErrada_Retorna401()
    {
        var auth = await AuthTestHelper.RegistrarNovaFamiliaAsync(Client, email: "login-errado@teste.com", senha: "SenhaCorreta1");

        var login = new LoginDto { Email = "login-errado@teste.com", Senha = "SenhaErrada1" };
        var response = await Client.PostAsJsonAsync("/api/auth/login", login, AuthTestHelper.JsonOptions);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.NotNull(auth); // só pra confirmar que o registro anterior funcionou
    }

    [Fact]
    public async Task Login_ComCredenciaisCorretas_RetornaToken()
    {
        await AuthTestHelper.RegistrarNovaFamiliaAsync(Client, email: "login-ok@teste.com", senha: "SenhaCorreta1");

        var login = new LoginDto { Email = "login-ok@teste.com", Senha = "SenhaCorreta1" };
        var response = await Client.PostAsJsonAsync("/api/auth/login", login, AuthTestHelper.JsonOptions);

        response.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task EndpointProtegido_SemToken_Retorna401()
    {
        var response = await Client.GetAsync("/api/pessoas");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Registrar_ComSenhaMenorQueOMinimo_Retorna400()
    {
        var dto = new RegistrarDto
        {
            Nome = "Usuário Teste",
            Email = $"{Guid.NewGuid():N}@teste.com",
            Senha = "abc123", // 6 caracteres — política atual exige 8
            ModoFamilia = ModoEntradaFamilia.Nova
        };

        var response = await Client.PostAsJsonAsync("/api/auth/registrar", dto, AuthTestHelper.JsonOptions);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Logout_RevogaToken_UsoPosteriorRetorna401()
    {
        var auth = await AuthTestHelper.RegistrarNovaFamiliaAsync(Client);
        Client.ComToken(auth.Token);

        var logoutResponse = await Client.PostAsync("/api/auth/logout", null);
        logoutResponse.EnsureSuccessStatusCode();

        var response = await Client.GetAsync("/api/pessoas");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ConfirmarEmail_ComTokenValido_Confirma()
    {
        var auth = await AuthTestHelper.RegistrarNovaFamiliaAsync(Client);

        using var scope = Factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<Usuario>>();
        var usuario = await userManager.FindByIdAsync(auth.Usuario.Id.ToString());
        var token = await userManager.GenerateEmailConfirmationTokenAsync(usuario!);

        var response = await Client.GetAsync($"/api/auth/confirmar-email?usuarioId={auth.Usuario.Id}&token={Uri.EscapeDataString(token)}");

        response.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task ConfirmarEmail_ComTokenInvalido_Retorna400()
    {
        var auth = await AuthTestHelper.RegistrarNovaFamiliaAsync(Client);

        var response = await Client.GetAsync($"/api/auth/confirmar-email?usuarioId={auth.Usuario.Id}&token=token-invalido");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task AtualizarPerfil_AlterandoNome_Persiste()
    {
        var auth = await AuthTestHelper.RegistrarNovaFamiliaAsync(Client);
        Client.ComToken(auth.Token);

        var dto = new AtualizarPerfilDto { Nome = "Nome Atualizado" };
        var response = await Client.PatchAsJsonAsync("/api/auth/me", dto, AuthTestHelper.JsonOptions);
        response.EnsureSuccessStatusCode();

        var envelope = await response.Content.ReadFromJsonAsync<ApiResponse<MeDto>>(AuthTestHelper.JsonOptions);
        Assert.Equal("Nome Atualizado", envelope!.Data!.Usuario.Nome);
    }

    [Fact]
    public async Task AtualizarPerfil_AlterandoEmail_MarcaComoNaoConfirmado()
    {
        var auth = await AuthTestHelper.RegistrarNovaFamiliaAsync(Client, email: $"{Guid.NewGuid():N}@teste.com");
        Client.ComToken(auth.Token);

        var novoEmail = $"{Guid.NewGuid():N}@teste.com";
        var dto = new AtualizarPerfilDto { Email = novoEmail };
        var response = await Client.PatchAsJsonAsync("/api/auth/me", dto, AuthTestHelper.JsonOptions);
        response.EnsureSuccessStatusCode();

        var envelope = await response.Content.ReadFromJsonAsync<ApiResponse<MeDto>>(AuthTestHelper.JsonOptions);
        Assert.Equal(novoEmail, envelope!.Data!.Usuario.Email);
        Assert.False(envelope.Data.Usuario.EmailConfirmado);
    }

    [Fact]
    public async Task ExportarDados_RetornaUsuarioFamiliaPessoasCategoriasETransacoes()
    {
        var auth = await AuthTestHelper.RegistrarNovaFamiliaAsync(Client);
        Client.ComToken(auth.Token);

        var pessoaResponse = await Client.PostAsJsonAsync("/api/pessoas", new PessoaCreateDto { Nome = "Filho", Idade = 10 }, AuthTestHelper.JsonOptions);
        pessoaResponse.EnsureSuccessStatusCode();

        var categoriaResponse = await Client.PostAsJsonAsync("/api/categorias", new CategoriaCreateDto { Descricao = "Mesada", Finalidade = FinalidadeCategoria.Despesa }, AuthTestHelper.JsonOptions);
        categoriaResponse.EnsureSuccessStatusCode();

        var response = await Client.GetAsync("/api/auth/exportar-dados");
        response.EnsureSuccessStatusCode();

        var envelope = await response.Content.ReadFromJsonAsync<ApiResponse<ExportacaoDadosDto>>(AuthTestHelper.JsonOptions);
        var dados = envelope!.Data!;

        Assert.Equal(auth.Usuario.Email, dados.Usuario.Email);
        Assert.Equal(auth.Familia.CodigoConvite, dados.Familia.CodigoConvite);
        Assert.Single(dados.Pessoas);
        Assert.Equal("Filho", dados.Pessoas[0].Nome);

        // A exportação traz só o que é dado do usuário: as categorias criadas
        // pela família. O catálogo do sistema fica de fora de propósito — ele
        // não pertence a ninguém e não é dado pessoal a portar (LGPD art. 18, V).
        var mesada = Assert.Single(dados.Categorias);
        Assert.Equal("Mesada", mesada.Descricao);
        Assert.Equal("Despesa", mesada.Finalidade);
    }

    [Fact]
    public async Task ExcluirConta_UnicoMembroDaFamilia_RemoveContaEDados()
    {
        var auth = await AuthTestHelper.RegistrarNovaFamiliaAsync(Client);
        Client.ComToken(auth.Token);

        var response = await Client.DeleteAsync("/api/auth/me");
        response.EnsureSuccessStatusCode();

        var meResponse = await Client.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, meResponse.StatusCode);
    }

    /// <summary>
    /// LGPD, art. 37 (bloco 4): o registro de auditoria não pode depender de
    /// FK para Usuario/Familia porque ExcluirConta apaga os dois na mesma
    /// transação em que a auditoria é gravada — este teste confirma que o
    /// registro sobrevive à exclusão.
    /// </summary>
    [Fact]
    public async Task ExcluirConta_UnicoMembroDaFamilia_RegistraAuditoriaMesmoApagandoUsuarioEFamilia()
    {
        var auth = await AuthTestHelper.RegistrarNovaFamiliaAsync(Client);
        Client.ComToken(auth.Token);

        var response = await Client.DeleteAsync("/api/auth/me");
        response.EnsureSuccessStatusCode();

        using var scope = Factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var registro = await context.RegistrosAuditoria
            .SingleOrDefaultAsync(r => r.Acao == "ExclusaoConta" && r.UsuarioId == auth.Usuario.Id);

        Assert.NotNull(registro);
        Assert.Equal(auth.Familia.Id, registro!.FamiliaId);
    }

    [Fact]
    public async Task ExcluirConta_FamiliaCompartilhada_MembroComum_RemoveSoAConta()
    {
        var admin = await AuthTestHelper.RegistrarNovaFamiliaAsync(Client, email: $"{Guid.NewGuid():N}@teste.com");

        var membroDto = new RegistrarDto
        {
            Nome = "Membro Comum",
            Email = $"{Guid.NewGuid():N}@teste.com",
            Senha = "Senha123",
            ModoFamilia = ModoEntradaFamilia.Entrar,
            CodigoConvite = admin.Familia.CodigoConvite
        };
        var membroResponse = await Client.PostAsJsonAsync("/api/auth/registrar", membroDto, AuthTestHelper.JsonOptions);
        membroResponse.EnsureSuccessStatusCode();
        var membroEnvelope = await membroResponse.Content.ReadFromJsonAsync<ApiResponse<AuthResponseDto>>(AuthTestHelper.JsonOptions);

        Client.ComToken(membroEnvelope!.Data!.Token);

        var deleteResponse = await Client.DeleteAsync("/api/auth/me");
        deleteResponse.EnsureSuccessStatusCode();

        // Dados da família continuam intactos para o admin.
        Client.ComToken(admin.Token);
        var pessoasResponse = await Client.GetAsync("/api/pessoas");
        pessoasResponse.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task ExcluirConta_UnicoAdminDeFamiliaCompartilhada_Retorna400()
    {
        var admin = await AuthTestHelper.RegistrarNovaFamiliaAsync(Client, email: $"{Guid.NewGuid():N}@teste.com");

        var membroDto = new RegistrarDto
        {
            Nome = "Membro Comum",
            Email = $"{Guid.NewGuid():N}@teste.com",
            Senha = "Senha123",
            ModoFamilia = ModoEntradaFamilia.Entrar,
            CodigoConvite = admin.Familia.CodigoConvite
        };
        var membroResponse = await Client.PostAsJsonAsync("/api/auth/registrar", membroDto, AuthTestHelper.JsonOptions);
        membroResponse.EnsureSuccessStatusCode();

        Client.ComToken(admin.Token);

        var response = await Client.DeleteAsync("/api/auth/me");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
