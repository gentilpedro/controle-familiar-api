using ControleFamiliarAPI.DTOs.Relatorios;

namespace ControleFamiliarAPI.Services.Interfaces
{
    public interface IRelatorioService
    {
        Task<ResumoPessoasDto> TotaisPorPessoa(CancellationToken cancellationToken = default);
        Task<List<TotaisCategoriaDto>> TotaisPorCategoria(CancellationToken cancellationToken = default);
        Task<byte[]> GerarExcelTotaisPessoa(CancellationToken cancellationToken = default);
        Task<byte[]> GerarExcelTotaisCategoria(CancellationToken cancellationToken = default);
    }

}