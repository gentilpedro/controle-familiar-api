using ControleFamiliarAPI.DTOs.PainelMensal;
using ControleFamiliarAPI.Responses;
using ControleFamiliarAPI.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OpenApi;

namespace ControleFamiliarAPI.Controllers
{
    [ApiController]
    [Route("api/painel-mensal")]
    [Authorize]
    public class PainelMensalController : ControllerBase
    {
        private readonly IPainelMensalService _service;

        public PainelMensalController(IPainelMensalService service)
        {
            _service = service;
        }

        [HttpGet]
        [Tags("Painel Mensal")]
        [EndpointSummary("Resumo financeiro de um mês")]
        [EndpointDescription("""
            Receitas/despesas confirmadas e pendentes do mês informado, saldo
            (receitas confirmadas menos despesas confirmadas — pendências não
            entram na conta) e se o mês já foi fechado.
            """)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult> ObterResumo([FromQuery] int ano, [FromQuery] int mes, CancellationToken cancellationToken)
        {
            return Ok(new ApiResponse<ResumoMensalDto>(await _service.ObterResumo(ano, mes, cancellationToken)));
        }

        [HttpPost("fechar")]
        [Tags("Painel Mensal")]
        [EndpointSummary("Fecha o mês e transporta o saldo pro mês seguinte")]
        [EndpointDescription("""
            Calcula o saldo confirmado do mês e, se diferente de zero, cria
            uma transação no primeiro dia do mês seguinte, na categoria de
            sistema "Saldo Anterior" — Receita se o saldo for positivo,
            Despesa se negativo. Um mês só pode ser fechado uma vez; fechar
            de novo dá 400. Não existe "reabrir" um mês fechado nesta versão
            — se um lançamento atrasado entrar depois, o saldo transportado
            fica desatualizado até uma correção manual.
            """)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult> Fechar(FecharMesDto dto, CancellationToken cancellationToken)
        {
            return Ok(new ApiResponse<ResumoMensalDto>(await _service.FecharMes(dto.Ano!.Value, dto.Mes!.Value, cancellationToken)));
        }
    }
}
