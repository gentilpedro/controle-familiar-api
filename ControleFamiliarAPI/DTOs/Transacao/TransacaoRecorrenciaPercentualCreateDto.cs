using System.ComponentModel.DataAnnotations;

namespace ControleFamiliarAPI.DTOs.Transacao
{
    /// <summary>
    /// Divide um valor total (ex.: salário) em ocorrências percentuais
    /// dentro de um mês — quinzena, fim de mês, ou qualquer outro recorte
    /// que o usuário defina. Só libera pra categoria com
    /// Categoria.AceitaDivisaoPercentual = true (hoje, só "Salário").
    /// </summary>
    public class TransacaoRecorrenciaPercentualCreateDto
    {
        [Required]
        [MaxLength(400)]
        public string Descricao { get; set; } = string.Empty;

        [Required]
        public decimal ValorTotal { get; set; }

        /// <summary>
        /// Ano e mês do lançamento — só essas duas partes importam, o dia é
        /// ignorado (cada ocorrência tem o próprio dia).
        /// </summary>
        [Required]
        public DateOnly? MesReferencia { get; set; }

        [Required]
        [MinLength(2, ErrorMessage = "Informe ao menos duas ocorrências — uma só não é divisão, é lançamento normal.")]
        public List<OcorrenciaPercentualDto> Ocorrencias { get; set; } = new();

        [Required]
        public int PessoaId { get; set; }

        [Required]
        public int CategoriaId { get; set; }
    }
}
