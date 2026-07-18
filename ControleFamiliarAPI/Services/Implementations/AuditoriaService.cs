using ControleFamiliarAPI.Data;
using ControleFamiliarAPI.Models;
using ControleFamiliarAPI.Services.Interfaces;

namespace ControleFamiliarAPI.Services.Implementations
{
    public class AuditoriaService : IAuditoriaService
    {
        private readonly AppDbContext _context;
        private readonly ICurrentUserService _currentUser;

        public AuditoriaService(AppDbContext context, ICurrentUserService currentUser)
        {
            _context = context;
            _currentUser = currentUser;
        }

        public async Task Registrar(string acao, int? usuarioAlvoId = null, CancellationToken cancellationToken = default)
        {
            _context.RegistrosAuditoria.Add(new RegistroAuditoria
            {
                UsuarioId = _currentUser.UsuarioId,
                FamiliaId = _currentUser.FamiliaId,
                UsuarioAlvoId = usuarioAlvoId,
                Acao = acao
            });

            await _context.SaveChangesAsync(cancellationToken);
        }
    }
}
