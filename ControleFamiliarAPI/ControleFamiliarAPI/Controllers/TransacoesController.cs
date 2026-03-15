using ControleFamiliarAPI.DTOs.Transacao;
using ControleFamiliarAPI.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace ControleFamiliarAPI.Controllers
{
    [ApiController]
    [Route("api/transacoes")]
    public class TransacoesController : ControllerBase
    {
        private readonly ITransacaoService _service;

        public TransacoesController(ITransacaoService service)
        {
            _service = service;
        }

        [HttpGet]
        public async Task<ActionResult> Listar()
        {
            return Ok(await _service.Listar());
        }

        [HttpPost]
        public async Task<ActionResult> Criar(TransacaoCreateDto dto)
        {
            await _service.Criar(dto);

            return Ok();
        }
    }
}