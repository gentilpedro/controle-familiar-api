using ControleFamiliarAPI.DTOs.Pessoa;

namespace ControleFamiliarAPI.Services.Interfaces
{
    public interface IPessoaService
    {
        Task<List<PessoaResponseDto>> Listar(CancellationToken cancellationToken = default);

        Task<PessoaResponseDto> Criar(PessoaCreateDto dto, CancellationToken cancellationToken = default);

        Task Atualizar(int id, PessoaUpdateDto dto, CancellationToken cancellationToken = default);

        Task Deletar(int id, CancellationToken cancellationToken = default);
    }
}
