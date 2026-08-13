using ControleFamiliarAPI.Models.Enums;

namespace ControleFamiliarAPI.DTOs.Transacao
{
    public class TransacaoResponseDto
    {
        public int Id { get; set; }
        public string Descricao { get; set; } = string.Empty;
        public decimal Valor { get; set; }
        public TipoTransacao Tipo { get; set; }
        public DateOnly Data { get; set; }
        public bool Pago { get; set; }
        public Guid? SerieId { get; set; }
        public int? NumeroParcela { get; set; }
        public int? TotalParcelas { get; set; }

        /// <summary>
        /// Nome/descrição já resolvidos, prontos pra exibir na tabela.
        /// PessoaId/CategoriaId (abaixo) existem à parte porque casar de
        /// volta pelo nome pra pré-selecionar a opção certa num formulário
        /// de edição é frágil — duas pessoas ou categorias podem ter o
        /// mesmo nome.
        /// </summary>
        public string Pessoa { get; set; } = string.Empty;
        public string Categoria { get; set; } = string.Empty;

        /// <summary>
        /// Nulo quando a transação não tem forma de pagamento — o campo é
        /// opcional e as transações anteriores a ele nunca tiveram uma.
        /// </summary>
        public string? FormaPagamento { get; set; }

        public int PessoaId { get; set; }
        public int CategoriaId { get; set; }
        public int? FormaPagamentoId { get; set; }
    }
}