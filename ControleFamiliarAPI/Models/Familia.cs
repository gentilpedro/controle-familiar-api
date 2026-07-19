using System.ComponentModel.DataAnnotations;
using ControleFamiliarAPI.Models.Enums;

namespace ControleFamiliarAPI.Models
{
    public class Familia
    {
        /// <summary>
        /// Identificador único gerado automaticamente.
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// Nome da família. Máximo de 200 caracteres.
        /// </summary>
        [Required]
        [MaxLength(200)]
        public string Nome { get; set; } = string.Empty;

        /// <summary>
        /// Código usado por outros usuários para entrar nesta família e
        /// compartilhar os mesmos dados (Pessoas, Categorias, Transações).
        /// </summary>
        [Required]
        [MaxLength(12)]
        public string CodigoConvite { get; set; } = string.Empty;

        public DateTime CriadoEm { get; set; } = DateTime.UtcNow;

        public List<Usuario> Usuarios { get; set; } = new();

        /// <summary>
        /// Id do Customer no Stripe associado a esta família - do membro
        /// que assinou o plano Família (normalmente o administrador).
        /// </summary>
        [MaxLength(64)]
        public string? StripeCustomerId { get; set; }

        /// <summary>
        /// Id da Subscription no Stripe do plano Família.
        /// </summary>
        [MaxLength(64)]
        public string? StripeSubscriptionIdFamilia { get; set; }

        /// <summary>
        /// Status atual da assinatura Família - enquanto Ativa, libera
        /// acesso pra todos os membros da família, não só quem assinou.
        /// Mantido em sincronia pelos webhooks do Stripe.
        /// </summary>
        public StatusAssinatura StatusAssinaturaFamilia { get; set; } = StatusAssinatura.Nenhuma;

        /// <summary>
        /// Até quando a assinatura Família vale, segundo o último evento
        /// de webhook recebido. O plano Família não tem trial.
        /// </summary>
        public DateTime? AssinaturaFamiliaValidaAte { get; set; }
    }
}
