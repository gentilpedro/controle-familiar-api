using ControleFamiliarAPI.DTOs.Transacao;
using ControleFamiliarAPI.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OpenApi;

namespace ControleFamiliarAPI.Controllers
{
    [ApiController]
    [Route("api/transacoes")]
    [Authorize]
    public class TransacoesController : ControllerBase
    {
        private readonly ITransacaoService _service;

        public TransacoesController(ITransacaoService service)
        {
            _service = service;
        }

        // GET api/transacoes
        [HttpGet]
        [Tags("Transa��es")]
        [EndpointSummary("Lista todas as transa��es financeiras")]
        [EndpointDescription("""
            Retorna todas as transa��es registradas no sistema.
            
            Cada transa��o cont�m:
            - Identificador da transa��o
            - Descri��o
            - Valor
            - Tipo (Receita ou Despesa)
            - Pessoa associada
            - Categoria associada
            """)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<ActionResult> Listar()
        {
            return Ok(await _service.Listar());
        }

        // POST api/transacoes
        [HttpPost]
        [Tags("Transa��es")]
        [EndpointSummary("Cria uma nova transa��o financeira")]
        [EndpointDescription("""
            Registra uma nova transa��o de receita ou despesa vinculada
            a uma pessoa e a uma categoria existente.
            
            Dados necess�rios:
            - Descri��o da transa��o
            - Valor (deve ser positivo)
            - Tipo da transa��o (Receita ou Despesa)
            - Identificador da pessoa
            - Identificador da categoria
            
            Regras de neg�cio:
            - O valor deve ser maior que zero
            - Pessoas menores de 18 anos podem registrar apenas despesas
            - A categoria deve ser compat�vel com o tipo da transa��o
            """)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult> Criar(TransacaoCreateDto dto)
        {
            await _service.Criar(dto);

            return Ok();
        }
    }
}