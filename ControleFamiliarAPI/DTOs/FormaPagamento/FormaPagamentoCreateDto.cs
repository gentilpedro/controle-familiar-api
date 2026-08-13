using System.ComponentModel.DataAnnotations;

namespace ControleFamiliarAPI.DTOs.FormaPagamento
{
    public class FormaPagamentoCreateDto
    {
        [Required]
        [MaxLength(100)]
        public string Descricao { get; set; } = string.Empty;
    }
}
