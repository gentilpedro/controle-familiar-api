using System.ComponentModel.DataAnnotations;
using ControleFamiliarAPI.Models.Enums;
using Microsoft.AspNetCore.Identity;

namespace ControleFamiliarAPI.Models
{
    public class Usuario : IdentityUser<int>
    {
        /// <summary>
        /// Nome de exibição do usuário.
        /// </summary>
        [Required]
        [MaxLength(200)]
        public string Nome { get; set; } = string.Empty;

        /// <summary>
        /// Família à qual este usuário pertence. Toda conta pertence a
        /// exatamente uma família — o uso "individual" é simplesmente
        /// uma família com um único membro.
        /// </summary>
        [Required]
        public int FamiliaId { get; set; }

        public Familia? Familia { get; set; }

        /// <summary>
        /// Administrador pode gerenciar os membros da família: remover,
        /// promover/rebaixar outros admins e regenerar o código de convite.
        /// Quem cria a família (modo "Nova" no cadastro) já nasce admin;
        /// quem entra por código de convite entra como membro comum.
        /// </summary>
        public bool EhAdministrador { get; set; }

        /// <summary>
        /// Id do Customer no Stripe associado a este usuário - só existe
        /// depois da primeira tentativa de assinatura Individual.
        /// </summary>
        [MaxLength(64)]
        public string? StripeCustomerId { get; set; }

        /// <summary>
        /// Id da Subscription no Stripe do plano Individual deste usuário.
        /// </summary>
        [MaxLength(64)]
        public string? StripeSubscriptionIdIndividual { get; set; }

        /// <summary>
        /// Status atual da assinatura Individual - mantido em sincronia
        /// pelos webhooks do Stripe (ver AssinaturaService), não é
        /// consultado ao vivo no Stripe a cada requisição.
        /// </summary>
        public StatusAssinatura StatusAssinaturaIndividual { get; set; } = StatusAssinatura.Nenhuma;

        /// <summary>
        /// Até quando a assinatura/trial Individual vale, segundo o último
        /// evento de webhook recebido.
        /// </summary>
        public DateTime? AssinaturaIndividualValidaAte { get; set; }

        /// <summary>
        /// Marca se este usuário já usou o trial gratuito do plano
        /// Individual alguma vez - o trial é concedido uma única vez por
        /// usuário, não uma janela fixa a partir do cadastro da conta.
        /// </summary>
        public bool TrialIndividualUsado { get; set; }
    }
}
