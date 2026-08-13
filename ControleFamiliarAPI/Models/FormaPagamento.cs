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
        /// Dia do mês em que a fatura fecha. Nulo quando esta forma não é
        /// cartão de crédito.
        /// </summary>
        // Guardado como dia (1 a 31), não como data: o ciclo se repete todo
        // mês. Dia que não existe no mês (29/30/31) cai no último dia dele,
        // mesma filosofia de clamping do parcelamento.
        public int? DiaFechamento { get; set; }

        /// <summary>
        /// Dia do mês em que a fatura vence. Nulo quando esta forma não é
        /// cartão de crédito.
        /// </summary>
        public int? DiaVencimento { get; set; }

        /// <summary>
        /// Categoria em que o pagamento da fatura é lançado (ex.: "Fatura
        /// Santander"). Só serve para o app reconhecer o pagamento que o
        /// usuário lança à mão — nada é gerado automaticamente.
        /// </summary>
        public int? CategoriaFaturaId { get; set; }
        public Categoria? CategoriaFatura { get; set; }

        /// <summary>
        /// Cartão de crédito: tem ciclo de fatura (fechamento e vencimento).
        /// </summary>
        // Os dois dias andam juntos — um só não descreve ciclo nenhum, e o
        // serviço recusa salvar apenas um deles.
        [NotMapped]
        public bool EhCartaoCredito => DiaFechamento is not null && DiaVencimento is not null;

        /// <summary>
        /// Lista de transações vinculadas a esta forma de pagamento.
        /// </summary>
        public List<Transacao> Transacoes { get; set; } = new();
    }
}
