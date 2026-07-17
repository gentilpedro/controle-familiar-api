namespace ControleFamiliarAPI.Services.Interfaces
{
    /// <summary>
    /// Expõe os dados do usuário autenticado na requisição atual,
    /// extraídos das claims do JWT.
    /// </summary>
    public interface ICurrentUserService
    {
        int UsuarioId { get; }

        int FamiliaId { get; }

        string Nome { get; }
    }
}
