using System.ComponentModel.DataAnnotations;

namespace ControleFamiliarAPI.DTOs.FormaPagamento
{
    public class FormaPagamentoUpdateDto
    {
        // Opcional de propósito, como no CategoriaUpdateDto: é um PATCH
        // parcial — o cliente manda só o que quer alterar.
        [MaxLength(100)]
        public string? Descricao { get; set; }

        [Range(1, 31)]
        public int? DiaFechamento { get; set; }

        [Range(1, 31)]
        public int? DiaVencimento { get; set; }

        public int? CategoriaFaturaId { get; set; }

        /// <summary>
        /// Desfaz a configuração de cartão (limpa fechamento, vencimento e
        /// categoria da fatura de uma vez). Existe pela mesma razão de
        /// TransacaoUpdateDto.RemoverFormaPagamento: num PATCH parcial,
        /// "campo ausente" e "campo null" chegam iguais, então sem a flag
        /// daria pra trocar o ciclo, nunca pra deixar de ser cartão. Tem
        /// precedência sobre os três campos acima.
        /// </summary>
        public bool RemoverCartao { get; set; }
    }
}
