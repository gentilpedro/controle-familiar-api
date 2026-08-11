using ControleFamiliarAPI.Models.Enums;
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
        [MinLength(8)]
        public string Senha { get; set; } = string.Empty;

        /// <summary>
        /// "Nova" para criar uma família própria (uso individual) ou "Entrar"
        /// para entrar em uma família existente via código de convite.
        /// </summary>
        // Anulável de propósito: num enum não-anulável, um corpo que omitisse o
        // campo cairia no valor 0 e o [Required] passaria batido — o cadastro
        // criaria uma família nova em silêncio. Anulável, a ausência é um 400.
        [Required]
        public ModoEntradaFamilia? ModoFamilia { get; set; }

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
