using ControleFamiliarAPI.DTO.Paginacao;
using ControleFamiliarAPI.DTOs.Transacao;

namespace ControleFamiliarAPI.Services.Interfaces
{
    public interface ITransacaoService
    {
        Task<PaginacaoResultado<TransacaoResponseDto>> Listar(int pagina, int tamanhoPagina);

        Task Criar(TransacaoCreateDto dto);
    }
}