using ControleFamiliarAPI.DTOs.Categoria;

namespace ControleFamiliarAPI.Services.Interfaces
{
    public interface ICategoriaService
    {
        Task<List<CategoriaResponseDto>> Listar(CancellationToken cancellationToken = default);
        Task<CategoriaResponseDto> Criar(CategoriaCreateDto dto, CancellationToken cancellationToken = default);
        Task Deletar(int id, CancellationToken cancellationToken = default);
    }
}
