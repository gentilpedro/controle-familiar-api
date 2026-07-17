using System.ComponentModel.DataAnnotations;

namespace ControleFamiliarAPI.DTOs.Auth
{
    public class RegistrarDto
    {
        [Required]
        [MaxLength(200)]
        public string Nome { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required]
        [MinLength(6)]
        public string Senha { get; set; } = string.Empty;

        /// <summary>
        /// "Nova" para criar uma família própria (uso individual) ou
        /// "Entrar" para entrar em uma família existente via código de convite.
        /// </summary>
        [Required]
        public string ModoFamilia { get; set; } = "Nova";

        /// <summary>
        /// Obrigatório quando ModoFamilia = "Nova".
        /// </summary>
        [MaxLength(200)]
        public string? NomeFamilia { get; set; }

        /// <summary>
        /// Obrigatório quando ModoFamilia = "Entrar".
        /// </summary>
        [MaxLength(12)]
        public string? CodigoConvite { get; set; }
    }
}
