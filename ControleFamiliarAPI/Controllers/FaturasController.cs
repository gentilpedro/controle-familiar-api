using ControleFamiliarAPI.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OpenApi;

namespace ControleFamiliarAPI.Controllers
{
    [ApiController]
    [Route("api/faturas")]
    [Authorize]
    public class FaturasController : ControllerBase
    {
        private readonly IFaturaService _service;

        public FaturasController(IFaturaService service)
        {
            _service = service;
        }

        [HttpGet]
        [Tags("Faturas")]
        [EndpointSummary("Faturas que vencem no mês informado")]
        [EndpointDescription("""
            Uma fatura por cartão de crédito da família (forma de pagamento
            com dia de fechamento e de vencimento configurados), com os
            lançamentos do ciclo, o total e as datas.

            O mês é o do **vencimento**, não o das compras: a fatura que vence
            em 10/09 costuma reunir compras de agosto.

            Nada aqui é gravado nem gerado — a fatura é calculada a partir das
            transações lançadas com aquela forma de pagamento. Pix, débito e
            dinheiro são outras formas de pagamento e não entram na fatura.
            """)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult> ListarPorVencimento([FromQuery] int ano, [FromQuery] int mes, CancellationToken cancellationToken)
        {
            return Ok(await _service.ListarPorVencimento(ano, mes, cancellationToken));
        }

        [HttpGet("abertas")]
        [Tags("Faturas")]
        [EndpointSummary("Fatura aberta de cada cartão")]
        [EndpointDescription("""
            A fatura que está acumulando agora em cada cartão: quanto já foi
            gasto no ciclo corrente, quando ele fecha e quando vence.
            """)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<ActionResult> ListarAbertas(CancellationToken cancellationToken)
        {
            return Ok(await _service.ListarAbertas(cancellationToken));
        }
    }
}
