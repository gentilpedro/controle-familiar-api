using ControleFamiliarAPI.Services.Interfaces;
using Microsoft.AspNetCore.Http;

namespace ControleFamiliarAPI.Services.Implementations
{
    public class CurrentUserService : ICurrentUserService
    {
        private readonly IHttpContextAccessor _httpContextAccessor;

        public CurrentUserService(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        private System.Security.Claims.ClaimsPrincipal User =>
            _httpContextAccessor.HttpContext?.User
                ?? throw new InvalidOperationException("Nenhum usuário autenticado no contexto atual.");

        public int UsuarioId =>
            int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                ?? throw new InvalidOperationException("Claim de usuário não encontrada."));

        public int FamiliaId =>
            int.Parse(User.FindFirst("familiaId")?.Value
                ?? throw new InvalidOperationException("Claim de família não encontrada."));

        public string Nome =>
            User.FindFirst("nome")?.Value ?? string.Empty;
    }
}
