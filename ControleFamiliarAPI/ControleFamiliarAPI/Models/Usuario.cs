using System.ComponentModel.DataAnnotations;

namespace ControleFamiliarAPI.Models
{
    public class Usuario
    {
        public int Id { get; set; }

        [Required]
        [MaxLength(200)]
        public string Nome { get; set; }

        [Required]
        [MaxLength(200)]
        public string Email { get; set; }

        [Required]
        public string SenhaHash { get; set; }
    }
}