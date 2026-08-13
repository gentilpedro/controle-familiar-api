using ControleFamiliarAPI.DTOs.Transacao;

namespace ControleFamiliarAPI.DTOs.Fatura
{
    /// <summary>
    /// Um ciclo de fatura de um cartão: o que foi lançado entre o fechamento
    /// anterior e o próximo, quanto soma e quando vence.
    /// </summary>
    // É um recorte calculado, não uma entidade: nada é gravado, nada precisa
    // ser "gerado" a cada mês. A fatura de setembro existe porque existem
    // lançamentos naquele intervalo, e some se eles forem apagados.
    public class FaturaDto
    {
        public int FormaPagamentoId { get; set; }

        /// <summary>Descrição do cartão (ex.: "Crédito Santander").</summary>
        public string FormaPagamento { get; set; } = string.Empty;

        /// <summary>Último dia que ainda entra nesta fatura.</summary>
        public DateOnly DataFechamento { get; set; }

        /// <summary>Dia de pagar.</summary>
        public DateOnly DataVencimento { get; set; }

        /// <summary>
        /// Primeiro dia do ciclo (o dia seguinte ao fechamento anterior) —
        /// serve pro cliente mostrar o intervalo por extenso.
        /// </summary>
        public DateOnly DataInicio { get; set; }

        /// <summary>
        /// Fatura já fechada: não entra mais lançamento nela, o total é
        /// final. Aberta significa que ainda está acumulando.
        /// </summary>
        public bool Fechada { get; set; }

        /// <summary>
        /// Soma das despesas menos as receitas do ciclo — estorno lançado no
        /// cartão abate a fatura, como no extrato de verdade.
        /// </summary>
        public decimal Total { get; set; }

        public int QuantidadeLancamentos { get; set; }

        public int? CategoriaFaturaId { get; set; }

        /// <summary>Categoria em que o pagamento desta fatura é lançado.</summary>
        public string? CategoriaFatura { get; set; }

        /// <summary>
        /// Quanto já foi lançado como despesa na categoria da fatura dentro
        /// do mês do vencimento. Nulo quando o cartão não tem categoria
        /// vinculada — sem ela não há como reconhecer o pagamento.
        /// </summary>
        // Nada é lançado automaticamente: este número existe só pra tela
        // conseguir dizer "já paguei / ainda não paguei / paguei valor
        // diferente" comparando com o Total.
        public decimal? TotalPagamentosLancados { get; set; }

        /// <summary>Lançamentos que compõem a fatura, do mais recente pro mais antigo.</summary>
        public List<TransacaoResponseDto> Lancamentos { get; set; } = new();
    }
}
