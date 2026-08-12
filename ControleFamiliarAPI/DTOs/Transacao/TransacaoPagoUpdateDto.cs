using System.ComponentModel.DataAnnotations;

namespace ControleFamiliarAPI.DTOs.Transacao
{
    public class TransacaoPagoUpdateDto
    {
        /// <summary>
        /// Nullable de propósito, mesmo padrão de RegistrarDto.Idade: em
        /// bool não-anulável, omitir o campo cairia silenciosamente em
        /// false — e nesse endpoint dedicado, ao contrário do Pago opcional
        /// de TransacaoCreateDto, um valor sempre é esperado no corpo.
        /// </summary>
        [Required]
        public bool? Pago { get; set; }
    }
}
