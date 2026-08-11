using System.Net;
using System.Net.Http.Headers;
using System.Text;
using ControleFamiliarAPI.Tests.Infrastructure;

namespace ControleFamiliarAPI.Tests.Integration;

/// <summary>
/// Garante que o contrato publicado no OpenAPI descreve o que a API aceita.
/// </summary>
// Estes testes existem por um problema real: ModoFamilia era string e o
// documento dizia apenas "string required", sem listar os valores válidos.
// Quem chamava a API direto (sem passar pelo frontend) não tinha como
// adivinhar "Nova"/"Entrar". O tipo virou enum e os comentários /// passaram a
// alimentar o schema — se qualquer um dos dois regredir, isto quebra.
public class OpenApiDocumentoTests : IntegrationTestBase
{
    private async Task<string> ObterDocumentoAsync()
    {
        var credenciais = Convert.ToBase64String(
            Encoding.UTF8.GetBytes($"{CustomWebApplicationFactory.UsuarioDocs}:{CustomWebApplicationFactory.SenhaDocs}"));

        Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", credenciais);

        var response = await Client.GetAsync("/openapi/v1.json");
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadAsStringAsync();
    }

    [Fact]
    public async Task Documento_ListaOsValoresValidosDeModoFamilia()
    {
        var documento = await ObterDocumentoAsync();

        Assert.Contains("\"Nova\"", documento);
        Assert.Contains("\"Entrar\"", documento);
    }

    [Fact]
    public async Task Documento_TrazAsDescricoesEscritasNosComentariosXml()
    {
        var documento = await ObterDocumentoAsync();

        // Trecho do /// <summary> de RegistrarDto.CodigoConvite. Se o
        // ComentariosXmlSchemaTransformer parar de ser aplicado (ou o XML
        // deixar de ser publicado junto), some daqui.
        Assert.Contains("Obrigatório quando ModoFamilia", documento);
    }

    [Fact]
    public async Task Documento_ExigeAutenticacaoForaDeDesenvolvimento()
    {
        var response = await Client.GetAsync("/openapi/v1.json");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
