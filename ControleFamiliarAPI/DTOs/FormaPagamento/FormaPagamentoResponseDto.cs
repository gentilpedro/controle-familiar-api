namespace ControleFamiliarAPI.DTOs.FormaPagamento
{
    public class FormaPagamentoResponseDto
    {
        public int Id { get; set; }

        public string Descricao { get; set; } = string.Empty;

        /// <summary>
        /// Forma de pagamento base do sistema: aparece para todas as famílias
        /// e não pode ser editada nem excluída. O cliente usa isto para não
        /// oferecer as ações que a API vai recusar com 403.
        /// </summary>
        public bool EhDoSistema { get; set; }
    }
}
