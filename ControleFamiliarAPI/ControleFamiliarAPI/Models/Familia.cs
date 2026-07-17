using System.ComponentModel.DataAnnotations;

namespace ControleGastos.Api.Models
{
    public class Familia
    {
        /// <summary>
        /// Identificador único gerado automaticamente.
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// Nome da família. Máximo de 200 caracteres.
        /// </summary>
        [Required]
        [MaxLength(200)]
        public string Nome { get; set; } = string.Empty;

        /// <summary>
        /// Código usado por outros usuários para entrar nesta família e
        /// compartilhar os mesmos dados (Pessoas, Categorias, Transações).
        /// </summary>
        [Required]
        [MaxLength(12)]
        public string CodigoConvite { get; set; } = string.Empty;

        public DateTime CriadoEm { get; set; } = DateTime.UtcNow;

        public List<Usuario> Usuarios { get; set; } = new();
    }
}
