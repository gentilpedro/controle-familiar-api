using ControleFamiliarAPI.DTOs.Auth;

namespace ControleFamiliarAPI.Services.Interfaces
{
    public interface IAuthService
    {
        Task<AuthResponseDto> Registrar(RegistrarDto dto);

        Task<AuthResponseDto> Login(LoginDto dto);

        Task<MeDto> Me();
    }
}
