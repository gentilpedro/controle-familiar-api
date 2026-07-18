namespace ControleFamiliarAPI.Services.Interfaces
{
    public interface IAuditoriaService
    {
        /// <summary>
        /// Registra uma operação sensível (LGPD, art. 37) atribuída ao
        /// usuário autenticado na requisição atual.
        /// </summary>
        /// <param name="acao">Identificador curto da ação (ex.: "ExclusaoConta").</param>
        /// <param name="usuarioAlvoId">Usuário afetado, quando a ação é sobre outra pessoa.</param>
        Task Registrar(string acao, int? usuarioAlvoId = null, CancellationToken cancellationToken = default);
    }
}
