using System.ComponentModel.DataAnnotations;

namespace ControleFamiliarAPI.DTOs.FormaPagamento
{
    public class FormaPagamentoUpdateDto
    {
        // Opcional de propósito, como no CategoriaUpdateDto: é um PATCH
        // parcial — o cliente manda só o que quer alterar.
        [MaxLength(100)]
        public string? Descricao { get; set; }
    }
}
