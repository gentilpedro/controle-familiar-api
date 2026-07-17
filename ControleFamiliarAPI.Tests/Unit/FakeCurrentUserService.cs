using ControleFamiliarAPI.Services.Interfaces;

namespace ControleFamiliarAPI.Tests.Unit;

public class FakeCurrentUserService : ICurrentUserService
{
    public int UsuarioId { get; init; } = 1;

    public int FamiliaId { get; init; } = 1;
}
