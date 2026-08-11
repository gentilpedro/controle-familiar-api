using System.ComponentModel.DataAnnotations;

namespace ControleFamiliarAPI.Models
{
    public class Pessoa
    {
        /// <summary>
        /// Identificador único gerado automaticamente.
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// Nome da pessoa. Máximo de 200 caracteres.
        /// </summary>
        [Required]
        [MaxLength(200)]
        public string Nome { get; set; } = string.Empty;

        /// <summary>
        /// Idade da pessoa.
        /// </summary>
        [Required]
        public int Idade { get; set; }

        /// <summary>
        /// Família à qual esta pessoa pertence.
        /// </summary>
        [Required]
        public int FamiliaId { get; set; }
        public Familia? Familia { get; set; }

        /// <summary>
        /// Conta que esta pessoa representa, quando ela é um membro da família.
        ///
        /// Nulo para pessoa cadastrada à mão — dependente sem login, que é
        /// justamente quem a regra de "menor de 18 não lança receita" atende.
        /// Cada conta tem no máximo uma Pessoa, garantido por índice único
        /// filtrado (ver AppDbContext).
        /// </summary>
        public int? UsuarioId { get; set; }
        public Usuario? Usuario { get; set; }

        /// <summary>
        /// Lista de transações vinculadas à pessoa.
        /// </summary>
        public List<Transacao> Transacoes { get; set; } = new();
    }
}