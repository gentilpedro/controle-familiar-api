using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ControleFamiliarAPI.Models.Enums;

namespace ControleFamiliarAPI.Models
{
    public class Categoria
    {
        /// <summary>
        /// Identificador único gerado automaticamente.
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// Descrição da categoria. Máximo de 400 caracteres.
        /// </summary>
        [Required]
        [MaxLength(400)]
        public string Descricao { get; set; } = string.Empty;

        /// <summary>
        /// Define se a categoria pode ser usada para receita, despesa ou ambas.
        /// </summary>
        [Required]
        public FinalidadeCategoria Finalidade { get; set; }

        /// <summary>
        /// Família dona desta categoria, ou <c>null</c> quando ela é do sistema.
        /// </summary>
        // Anulável de propósito: as categorias base (Água, Luz, Mercado...) não
        // pertencem a ninguém — existem uma única vez e ficam disponíveis para
        // todas as famílias. FamiliaId preenchido significa categoria criada
        // por uma família, visível só para ela.
        public int? FamiliaId { get; set; }
        public Familia? Familia { get; set; }

        /// <summary>
        /// Categoria base do sistema, disponível para todos e sem dono.
        /// </summary>
        [NotMapped]
        public bool EhDoSistema => FamiliaId is null;

        /// <summary>
        /// Libera o fluxo de transação recorrente por percentual (ex.:
        /// salário dividido em quinzenas). Não é exposto como opção editável
        /// pra categoria de família — como categoria de sistema é imutável,
        /// isso trava o fluxo à categoria "Salário" com segurança, sem
        /// comparar nome de categoria em lugar nenhum.
        /// </summary>
        public bool AceitaDivisaoPercentual { get; set; }

        /// <summary>
        /// Lista de transações vinculadas à categoria.
        /// </summary>
        public List<Transacao> Transacoes { get; set; } = new();
    }
}