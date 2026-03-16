using ControleFamiliarAPI.DTO.Auth;

public interface IAuthService
{
    Task<AuthResponseDto> Register(RegisterDto dto);    
    Task<AuthResponseDto> Login(LoginDto dto);
}