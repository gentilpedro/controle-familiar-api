using ControleFamiliarAPI.DTOs.PainelMensal;

namespace ControleFamiliarAPI.Services.Interfaces
{
    public interface IPainelMensalService
    {
        Task<ResumoMensalDto> ObterResumo(int ano, int mes, CancellationToken cancellationToken = default);

        Task<ResumoMensalDto> FecharMes(int ano, int mes, CancellationToken cancellationToken = default);
    }
}
