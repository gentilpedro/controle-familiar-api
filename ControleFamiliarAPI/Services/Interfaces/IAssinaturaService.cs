using ControleFamiliarAPI.DTOs.Assinatura;
using ControleFamiliarAPI.Models.Enums;
using Stripe;

namespace ControleFamiliarAPI.Services.Interfaces
{
    public interface IAssinaturaService
    {
        Task<CheckoutResponseDto> CriarCheckoutSession(TipoPlano tipoPlano, CancellationToken cancellationToken = default);

        Task<AssinaturaStatusDto> ObterStatus(CancellationToken cancellationToken = default);

        Task<PortalResponseDto> CriarPortalSession(CancellationToken cancellationToken = default);

        Task ProcessarWebhookAsync(Event stripeEvent, CancellationToken cancellationToken = default);
    }
}
