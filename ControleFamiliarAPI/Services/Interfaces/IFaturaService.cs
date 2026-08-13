using ControleFamiliarAPI.DTOs.Fatura;

namespace ControleFamiliarAPI.Services.Interfaces
{
    public interface IFaturaService
    {
        /// <summary>
        /// Faturas que <b>vencem</b> no mês informado, uma por cartão da
        /// família. O mês é o do vencimento, não o das compras: a fatura que
        /// vence em 10/09 costuma ser de compras de agosto.
        /// </summary>
        Task<List<FaturaDto>> ListarPorVencimento(int ano, int mes, CancellationToken cancellationToken = default);

        /// <summary>
        /// Fatura aberta de cada cartão — a que está acumulando agora e
        /// ainda vai fechar. É o "quanto já gastei neste ciclo".
        /// </summary>
        Task<List<FaturaDto>> ListarAbertas(CancellationToken cancellationToken = default);
    }
}
