using ControleFamiliarAPI.DTOs.Auth;

namespace ControleFamiliarAPI.Services.Interfaces
{
    public interface IFamiliaService
    {
        Task<FamiliaDto> Obter(CancellationToken cancellationToken = default);

        Task<FamiliaDto> RemoverMembro(int usuarioId, CancellationToken cancellationToken = default);

        Task<FamiliaDto> PromoverAdmin(int usuarioId, CancellationToken cancellationToken = default);

        Task<FamiliaDto> RebaixarAdmin(int usuarioId, CancellationToken cancellationToken = default);

        Task<FamiliaDto> RegenerarCodigoConvite(CancellationToken cancellationToken = default);

        Task ConvidarPorEmail(string email, CancellationToken cancellationToken = default);

        /// <summary>
        /// Histórico de quem entrou e saiu da família (criação, entrada,
        /// remoção, exclusão de conta), mais recente primeiro.
        /// </summary>
        Task<List<HistoricoFamiliaItemDto>> ObterHistorico(CancellationToken cancellationToken = default);
    }
}
