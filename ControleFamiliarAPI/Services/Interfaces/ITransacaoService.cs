using ControleFamiliarAPI.DTOs.Paginacao;
using ControleFamiliarAPI.DTOs.Transacao;

namespace ControleFamiliarAPI.Services.Interfaces
{
    public interface ITransacaoService
    {
        Task<PaginacaoResultado<TransacaoResponseDto>> Listar(int pagina, int tamanhoPagina, CancellationToken cancellationToken = default);

        Task Criar(TransacaoCreateDto dto, CancellationToken cancellationToken = default);

        Task CriarParcelada(TransacaoParceladaCreateDto dto, CancellationToken cancellationToken = default);

        Task Atualizar(int id, TransacaoUpdateDto dto, CancellationToken cancellationToken = default);

        Task Deletar(int id, bool excluirFuturas = false, CancellationToken cancellationToken = default);
    }
}
