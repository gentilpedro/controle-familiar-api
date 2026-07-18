using ControleFamiliarAPI.DTOs.Auth;
using ControleFamiliarAPI.Models;

namespace ControleFamiliarAPI.Services.Interfaces
{
    /// <summary>
    /// Monta FamiliaDto e gera códigos de convite únicos — lógica
    /// compartilhada entre AuthService (cadastro) e FamiliaService (gestão
    /// da família), antes duplicada quase idêntica nos dois.
    /// </summary>
    public interface IFamiliaDtoFactory
    {
        Task<string> GerarCodigoConviteUnico(CancellationToken cancellationToken = default);

        Task<FamiliaDto> MontarFamiliaDto(Familia familia, CancellationToken cancellationToken = default);
    }
}
