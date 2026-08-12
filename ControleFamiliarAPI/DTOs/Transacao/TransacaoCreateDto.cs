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

        [Required]
        public int PessoaId { get; set; }

        [Required]
        public int CategoriaId { get; set; }
    }
}