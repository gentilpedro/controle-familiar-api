using ControleFamiliarAPI.Models.Enums;
using System.ComponentModel.DataAnnotations;

namespace ControleFamiliarAPI.DTOs.Transacao
{
    public class TransacaoParceladaCreateDto
    {
        [Required]
        [MaxLength(400)]
        public string Descricao { get; set; } = string.Empty;

        /// <summary>
        /// Valor total da compra, dividido igualmente entre as parcelas — a
        /// última absorve o resíduo do arredondamento, então a soma das
        /// parcelas sempre bate com este valor.
        /// </summary>
        [Required]
        public decimal ValorTotal { get; set; }

        [Required]
        [Range(2, 60, ErrorMessage = "Número de parcelas deve estar entre 2 e 60.")]
        public int NumeroParcelas { get; set; }

        [Required]
        public TipoTransacao Tipo { get; set; }

        /// <summary>
        /// Data da primeira parcela. As demais caem no mesmo dia dos meses
        /// seguintes — se o dia não existir em algum mês (29/30/31), cai no
        /// último dia daquele mês (comportamento nativo de DateOnly.AddMonths).
        /// </summary>
        [Required]
        public DateOnly? DataPrimeiraParcela { get; set; }

        [Required]
        public int PessoaId { get; set; }

        [Required]
        public int CategoriaId { get; set; }
    }
}
