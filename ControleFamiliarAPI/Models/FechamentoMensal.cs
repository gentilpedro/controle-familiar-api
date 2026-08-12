using System.ComponentModel.DataAnnotations;

namespace ControleFamiliarAPI.Models
{
    /// <summary>
    /// Registro de que um mês foi fechado — o saldo confirmado dele (receitas
    /// recebidas menos despesas pagas) foi transportado pro mês seguinte como
    /// uma transação normal. Existe pra impedir fechar o mesmo mês duas
    /// vezes (índice único FamiliaId+Mes) e pra guardar quando/quanto foi
    /// transportado, mesmo que o usuário edite as transações do mês depois.
    /// </summary>
    public class FechamentoMensal
    {
        public int Id { get; set; }

        [Required]
        public int FamiliaId { get; set; }
        public Familia? Familia { get; set; }

        /// <summary>
        /// Mês fechado, sempre com dia 1 — mesma convenção de
        /// TransacaoRecorrenciaPercentualCreateDto.MesReferencia.
        /// </summary>
        [Required]
        public DateOnly Mes { get; set; }

        [Required]
        public decimal SaldoTransportado { get; set; }

        /// <summary>
        /// A transação de saldo criada no mês seguinte. Nula quando o saldo
        /// do mês fechado é exatamente zero — não faz sentido lançar uma
        /// transação de R$0,00 só pra ter uma.
        /// </summary>
        public int? TransacaoGeradaId { get; set; }
        public Transacao? TransacaoGerada { get; set; }

        public DateTime FechadoEm { get; set; } = DateTime.UtcNow;
    }
}
