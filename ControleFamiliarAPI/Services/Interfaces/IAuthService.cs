using ControleFamiliarAPI.DTOs.Auth;

namespace ControleFamiliarAPI.Services.Interfaces
{
    public interface IAuthService
    {
        Task<AuthResponseDto> Registrar(RegistrarDto dto, CancellationToken cancellationToken = default);

        Task<AuthResponseDto> Login(LoginDto dto, CancellationToken cancellationToken = default);

        Task<MeDto> Me(CancellationToken cancellationToken = default);

        Task Logout(CancellationToken cancellationToken = default);

        Task ConfirmarEmail(int usuarioId, string token, CancellationToken cancellationToken = default);
    }
}
