using System.Text.Json.Serialization;

namespace ControleFamiliarAPI.Models.Enums
{
    /// <summary>
    /// Como o usuário entra numa família ao se cadastrar.
    /// </summary>
    // Serializado como string ("Nova"/"Entrar"), e não como número. O atributo
    // fica no tipo de propósito, em vez de um JsonStringEnumConverter global:
    // TipoTransacao e FinalidadeCategoria trafegam como número hoje, e o
    // frontend depende disso (envia `tipo: 1`) — ligar o conversor globalmente
    // quebraria o cadastro de transações e de categorias.
    //
    // O conversor aceita string na leitura e também o número correspondente,
    // então clientes antigos que mandassem 1/2 continuam funcionando.
    [JsonConverter(typeof(JsonStringEnumConverter<ModoEntradaFamilia>))]
    public enum ModoEntradaFamilia
    {
        /// <summary>Cria uma família nova; quem se cadastra vira administrador dela.</summary>
        Nova = 1,

        /// <summary>Entra numa família existente usando o código de convite.</summary>
        Entrar = 2
    }
}
