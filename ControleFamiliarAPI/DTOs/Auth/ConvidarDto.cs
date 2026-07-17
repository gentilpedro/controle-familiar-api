using System.ComponentModel.DataAnnotations;

namespace ControleFamiliarAPI.DTOs.Auth
{
    public class ConvidarDto
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;
    }
}
