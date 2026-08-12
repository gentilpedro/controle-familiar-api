using ControleFamiliarAPI.Models.Enums;
using System.ComponentModel.DataAnnotations;

namespace ControleFamiliarAPI.DTOs.Transacao
{
    public class TransacaoCreateDto
    {
        [Required]
        [MaxLength(400)]
        public string Descricao { get; set; } = string.Empty;

        [Required]
        public decimal Valor { get; set; }

        [Required]
        public TipoTransacao Tipo { get; set; }

        /// <summary>
        /// Data efetiva da transação. Nullable de propósito, mesmo padrão de
        /// RegistrarDto.Idade: em DateOnly não-nulo, omitir o campo cairia
        /// silenciosamente em 0001-01-01 em vez de dar 400 — [Required] só
        /// pega ausência de verdade quando o tipo é anulável.
        /// </summary>
        [Required]
        public DateOnly? Data { get; set; }

        /// <summary>
        /// Já confirmada (paga/recebida)? Omitir cai em true — diferente de
        /// Data/Idade, aqui a ausência tem um default sensato (o comum é
        /// registrar algo que já aconteceu), não é um esquecimento perigoso.
        /// </summary>
        public bool? Pago { get; set; }

        [Required]
        public int PessoaId { get; set; }

        [Required]
        public int CategoriaId { get; set; }
    }
}