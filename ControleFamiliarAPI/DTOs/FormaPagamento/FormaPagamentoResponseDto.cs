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

        /// <summary>
        /// Cartão de crédito: tem ciclo de fatura. Equivale a
        /// DiaFechamento e DiaVencimento preenchidos.
        /// </summary>
        public bool EhCartaoCredito { get; set; }

        public int? DiaFechamento { get; set; }

        public int? DiaVencimento { get; set; }

        public int? CategoriaFaturaId { get; set; }

        /// <summary>Descrição já resolvida da categoria da fatura, pronta pra exibir.</summary>
        public string? CategoriaFatura { get; set; }
    }
}
