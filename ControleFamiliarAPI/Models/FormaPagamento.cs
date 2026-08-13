using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ControleFamiliarAPI.Models
{
    /// <summary>
    /// Como o dinheiro entrou ou saiu (Pix, Dinheiro, Saque...). Complementa
    /// a Categoria, que responde "com o quê" — esta responde "por onde".
    /// </summary>
    public class FormaPagamento
    {
        /// <summary>
        /// Identificador único gerado automaticamente.
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// Descrição da forma de pagamento. Máximo de 100 caracteres.
        /// </summary>
        [Required]
        [MaxLength(100)]
        public string Descricao { get; set; } = string.Empty;

        /// <summary>
        /// Família dona desta forma de pagamento, ou <c>null</c> quando ela é
        /// do sistema.
        /// </summary>
        // Mesmo desenho de Categoria.FamiliaId: as do sistema (Pix, Dinheiro,
        // Saque) não pertencem a ninguém — existem uma única vez e ficam
        // disponíveis para todas as famílias.
        public int? FamiliaId { get; set; }
        public Familia? Familia { get; set; }

        /// <summary>
        /// Forma de pagamento base do sistema, disponível para todos e sem dono.
        /// </summary>
        [NotMapped]
        public bool EhDoSistema => FamiliaId is null;

        /// <summary>
        /// Lista de transações vinculadas a esta forma de pagamento.
        /// </summary>
        public List<Transacao> Transacoes { get; set; } = new();
    }
}
