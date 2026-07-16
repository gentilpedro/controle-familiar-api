using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;

namespace ControleGastos.Api.Models
{
    public class Usuario : IdentityUser<int>
    {
        /// <summary>
        /// Nome de exibição do usuário.
        /// </summary>
        [Required]
        [MaxLength(200)]
        public string Nome { get; set; } = string.Empty;

        /// <summary>
        /// Família à qual este usuário pertence. Toda conta pertence a
        /// exatamente uma família — o uso "individual" é simplesmente
        /// uma família com um único membro.
        /// </summary>
        [Required]
        public int FamiliaId { get; set; }

        public Familia? Familia { get; set; }
    }
}
