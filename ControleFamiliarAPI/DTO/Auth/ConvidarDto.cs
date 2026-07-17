using System.ComponentModel.DataAnnotations;

namespace ControleFamiliarAPI.DTO.Auth
{
    public class ConvidarDto
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;
    }
}
