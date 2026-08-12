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
        /// <param name="nomeAlvo">
        /// Nome de quem foi afetado, no momento da ação — denormalizado porque
        /// usuarioAlvoId pode apontar pra uma conta já excluída.
        /// </param>
        /// <param name="cancellationToken"></param>
        Task Registrar(string acao, int? usuarioAlvoId = null, string? nomeAlvo = null, CancellationToken cancellationToken = default);
    }
}
