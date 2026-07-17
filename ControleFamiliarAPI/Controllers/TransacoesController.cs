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
        [Tags("Transações")]
        [EndpointSummary("Lista as transações financeiras da família, paginado")]
        [EndpointDescription("""
            Retorna as transações da família do usuário autenticado, da mais
            recente para a mais antiga, paginadas.

            Cada transação contém:
            - Identificador da transação
            - Descrição
            - Valor
            - Tipo (Receita ou Despesa)
            - Pessoa associada
            - Categoria associada

            Parâmetros de paginação (query string):
            - pagina (padrão 1)
            - tamanhoPagina (padrão 50, máximo 200)
            """)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<ActionResult> Listar([FromQuery] int pagina = 1, [FromQuery] int tamanhoPagina = 50)
        {
            return Ok(await _service.Listar(pagina, tamanhoPagina));
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