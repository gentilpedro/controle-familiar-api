namespace ControleFamiliarAPI.DTOs.PainelMensal
{
    public class ResumoMensalDto
    {
        public decimal TotalReceitasConfirmadas { get; set; }
        public decimal TotalReceitasPendentes { get; set; }
        public decimal TotalDespesasConfirmadas { get; set; }
        public decimal TotalDespesasPendentes { get; set; }

        /// <summary>
        /// Receitas confirmadas menos despesas confirmadas — não conta
        /// pendências, de propósito (saldo é "o que já entrou/saiu de
        /// verdade", não uma projeção do que ainda vai acontecer).
        /// </summary>
        public decimal Saldo => TotalReceitasConfirmadas - TotalDespesasConfirmadas;

        public bool MesFechado { get; set; }
        public DateTime? FechadoEm { get; set; }
    }
}
