using ControleFamiliarAPI.Models.Enums;
using System.ComponentModel.DataAnnotations;

namespace ControleFamiliarAPI.DTOs.Transacao
{
    public class TransacaoUpdateDto
    {
        // Campos opcionais de propósito: é um PATCH parcial (mesmo padrão de
        // PessoaUpdateDto) — o cliente só envia o que quer alterar.
        [MaxLength(400)]
        public string? Descricao { get; set; }

        public decimal? Valor { get; set; }

        public DateOnly? Data { get; set; }

        /// <summary>
        /// Nunca propaga com AplicarAFuturas, mesmo raciocínio de Data: o
        /// status de pagamento é por ocorrência, editar uma não deveria
        /// marcar as outras como pagas/recebidas junto.
        /// </summary>
        public bool? Pago { get; set; }

        public TipoTransacao? Tipo { get; set; }

        public int? PessoaId { get; set; }

        public int? CategoriaId { get; set; }

        /// <summary>
        /// Quando true e a transação pertence a uma série (parcelamento ou
        /// divisão percentual), propaga os campos alterados — exceto Data —
        /// para as ocorrências seguintes da mesma série. Sem efeito numa
        /// transação avulsa.
        /// </summary>
        public bool AplicarAFuturas { get; set; }
    }
}
