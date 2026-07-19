using ControleFamiliarAPI.DTOs.Assinatura;
using ControleFamiliarAPI.Models.Enums;

namespace ControleFamiliarAPI.Services.Interfaces
{
    public interface IAssinaturaService
    {
        Task<CheckoutResponseDto> CriarCheckoutSession(TipoPlano tipoPlano, CancellationToken cancellationToken = default);

        Task<AssinaturaStatusDto> ObterStatus(CancellationToken cancellationToken = default);
    }
}
