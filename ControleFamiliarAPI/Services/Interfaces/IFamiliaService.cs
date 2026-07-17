using ControleFamiliarAPI.DTOs.Auth;

namespace ControleFamiliarAPI.Services.Interfaces
{
    public interface IFamiliaService
    {
        Task<FamiliaDto> Obter();

        Task<FamiliaDto> RemoverMembro(int usuarioId);

        Task<FamiliaDto> PromoverAdmin(int usuarioId);

        Task<FamiliaDto> RebaixarAdmin(int usuarioId);

        Task<FamiliaDto> RegenerarCodigoConvite();

        Task ConvidarPorEmail(string email);
    }
}
