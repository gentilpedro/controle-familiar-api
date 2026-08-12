using System.ComponentModel.DataAnnotations;

namespace ControleFamiliarAPI.DTOs.Transacao
{
    /// <summary>
    /// Um recorte percentual do valor total dentro do mês de referência —
    /// ex.: primeira quinzena (dia 15, 35%) e fim do mês (dia 30, 75%).
    /// </summary>
    public class OcorrenciaPercentualDto
    {
        /// <summary>Dia do mês de referência em que esta parte cai.</summary>
        [Required]
        [Range(1, 31)]
        public int? Dia { get; set; }

        /// <summary>
        /// Percentual do valor total. Não precisa somar 100 entre as
        /// ocorrências — adiantamento e saldo podem vir de bases diferentes.
        /// </summary>
        [Required]
        [Range(0.01, 1000)]
        public decimal? Percentual { get; set; }
    }
}
