using System.ComponentModel.DataAnnotations;

namespace ControleFamiliarAPI.DTOs.PainelMensal
{
    public class FecharMesDto
    {
        [Required]
        [Range(2000, 2100)]
        public int? Ano { get; set; }

        [Required]
        [Range(1, 12)]
        public int? Mes { get; set; }
    }
}
