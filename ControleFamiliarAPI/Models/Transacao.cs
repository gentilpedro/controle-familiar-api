using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ControleFamiliarAPI.Models.Enums;

namespace ControleFamiliarAPI.Models
{
    public class Transacao
    {
        /// <summary>
        /// Identificador único gerado automaticamente.
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// Descrição da transação. Máximo de 400 caracteres.
        /// </summary>
        [Required]
        [MaxLength(400)]
        public string Descricao { get; set; } = string.Empty;

        /// <summary>
        /// Valor da transação. Deve ser positivo.
        /// </summary>
        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal Valor { get; set; }

        /// <summary>
        /// Tipo da transação: Receita ou Despesa.
        /// </summary>
        [Required]
        public TipoTransacao Tipo { get; set; }

        /// <summary>
        /// Data efetiva da transação. Distinta de "quando foi cadastrada" —
        /// é o que permite uma parcela de compra parcelada cair em outubro
        /// mesmo que o parcelamento inteiro tenha sido criado em agosto.
        /// </summary>
        [Required]
        public DateOnly Data { get; set; }

        /// <summary>
        /// Confirmada — "paga" para Despesa, "recebida" para Receita (mesmo
        /// campo, rótulo contextual: não são dois booleanos). Transação
        /// avulsa nasce true por padrão (o comum é registrar algo que já
        /// aconteceu); ocorrência de série (parcelamento, recorrência
        /// percentual) nasce sempre false — é uma obrigação futura até o
        /// usuário confirmar.
        /// </summary>
        [Required]
        public bool Pago { get; set; } = true;

        /// <summary>
        /// Identifica o grupo de transações nascidas juntas de um
        /// parcelamento ou de uma divisão percentual (ex.: salário
        /// quinzenal). Nulo para transação avulsa.
        /// </summary>
        public Guid? SerieId { get; set; }

        /// <summary>
        /// Posição (1-based) desta transação dentro da série. Nulo fora de
        /// uma série.
        /// </summary>
        public int? NumeroParcela { get; set; }

        /// <summary>
        /// Total de transações que a série tinha ao ser criada. Não é
        /// recalculado se uma ocorrência for excluída depois — "3/10" pode
        /// continuar mostrando 10 mesmo com só 8 restantes.
        /// </summary>
        public int? TotalParcelas { get; set; }

        /// <summary>
        /// Pessoa relacionada à transação.
        /// </summary>
        [Required]
        public int PessoaId { get; set; }
        public Pessoa? Pessoa { get; set; }

        /// <summary>
        /// Categoria relacionada à transação.
        /// </summary>
        [Required]
        public int CategoriaId { get; set; }
        public Categoria? Categoria { get; set; }

        /// <summary>
        /// Forma de pagamento usada (Pix, Dinheiro, Saque...). Anulável de
        /// propósito: o campo nasceu depois das transações que já existiam, e
        /// nem todo lançamento tem uma forma que o usuário queira registrar —
        /// exigir uma agora inventaria dado para o histórico inteiro.
        /// </summary>
        public int? FormaPagamentoId { get; set; }
        public FormaPagamento? FormaPagamento { get; set; }

        /// <summary>
        /// Família à qual esta transação pertence (herdada da Pessoa no momento da criação).
        /// </summary>
        [Required]
        public int FamiliaId { get; set; }
        public Familia? Familia { get; set; }
    }
}