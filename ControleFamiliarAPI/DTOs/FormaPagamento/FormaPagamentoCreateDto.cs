using System.ComponentModel.DataAnnotations;

namespace ControleFamiliarAPI.DTOs.FormaPagamento
{
    public class FormaPagamentoCreateDto
    {
        [Required]
        [MaxLength(100)]
        public string Descricao { get; set; } = string.Empty;

        /// <summary>
        /// Dia em que a fatura fecha (1 a 31). Vai junto com DiaVencimento —
        /// os dois preenchidos transformam esta forma em cartão de crédito,
        /// os dois vazios são o caso comum (Pix, dinheiro, débito).
        /// </summary>
        [Range(1, 31)]
        public int? DiaFechamento { get; set; }

        /// <summary>Dia em que a fatura vence (1 a 31).</summary>
        [Range(1, 31)]
        public int? DiaVencimento { get; set; }

        /// <summary>
        /// Categoria em que o pagamento da fatura é lançado (ex.: "Fatura
        /// Santander"). Só faz sentido em cartão de crédito.
        /// </summary>
        public int? CategoriaFaturaId { get; set; }
    }
}
