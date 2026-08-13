using ControleFamiliarAPI.DTOs.Paginacao;
using ControleFamiliarAPI.DTOs.Transacao;

namespace ControleFamiliarAPI.Services.Interfaces
{
    public interface ITransacaoService
    {
        /// <summary>
        /// Lista as transações da família, paginadas. O par ano/mes filtra por
        /// período — os dois juntos ou nenhum (um sem o outro é 400); sem eles,
        /// a listagem traz o histórico inteiro.
        /// </summary>
        Task<PaginacaoResultado<TransacaoResponseDto>> Listar(int pagina, int tamanhoPagina, int? ano = null, int? mes = null, CancellationToken cancellationToken = default);

        Task Criar(TransacaoCreateDto dto, CancellationToken cancellationToken = default);

        Task CriarParcelada(TransacaoParceladaCreateDto dto, CancellationToken cancellationToken = default);

        Task CriarRecorrenciaPercentual(TransacaoRecorrenciaPercentualCreateDto dto, CancellationToken cancellationToken = default);

        Task Atualizar(int id, TransacaoUpdateDto dto, CancellationToken cancellationToken = default);

        Task MarcarPago(int id, bool pago, CancellationToken cancellationToken = default);

        Task Deletar(int id, bool excluirFuturas = false, CancellationToken cancellationToken = default);
    }
}
