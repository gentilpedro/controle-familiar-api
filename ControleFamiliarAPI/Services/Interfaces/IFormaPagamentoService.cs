using ControleFamiliarAPI.DTOs.FormaPagamento;

namespace ControleFamiliarAPI.Services.Interfaces
{
    public interface IFormaPagamentoService
    {
        Task<List<FormaPagamentoResponseDto>> Listar(CancellationToken cancellationToken = default);
        Task<FormaPagamentoResponseDto> Criar(FormaPagamentoCreateDto dto, CancellationToken cancellationToken = default);
        Task<FormaPagamentoResponseDto> Atualizar(int id, FormaPagamentoUpdateDto dto, CancellationToken cancellationToken = default);
        Task Deletar(int id, CancellationToken cancellationToken = default);
    }
}
