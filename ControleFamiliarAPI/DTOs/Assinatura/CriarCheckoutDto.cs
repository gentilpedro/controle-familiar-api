using System.ComponentModel.DataAnnotations;
using ControleFamiliarAPI.Models.Enums;

namespace ControleFamiliarAPI.DTOs.Assinatura
{
    public class CriarCheckoutDto
    {
        [Required]
        public TipoPlano TipoPlano { get; set; }
    }
}
