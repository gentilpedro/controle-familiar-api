using ControleFamiliarAPI.DTOs.Transacao;
using ControleFamiliarAPI.Filters;
using ControleFamiliarAPI.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OpenApi;

namespace ControleFamiliarAPI.Controllers
{
    [ApiController]
    [Route("api/transacoes")]
    [Authorize]
    [ExigirAssinatura]
    public class TransacoesController : ControllerBase
    {
        private readonly ITransacaoService _service;

        public TransacoesController(ITransacaoService service)
        {
            _service = service;
        }

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
        public async Task<ActionResult> Listar([FromQuery] int pagina = 1, [FromQuery] int tamanhoPagina = 50, CancellationToken cancellationToken = default)
        {
            return Ok(await _service.Listar(pagina, tamanhoPagina, cancellationToken));
        }

        [HttpPost]
        [Tags("Transações")]
        [EndpointSummary("Cria uma nova transação financeira")]
        [EndpointDescription("""
            Registra uma nova transação de receita ou despesa vinculada
            a uma pessoa e a uma categoria existente.

            Dados necessários:
            - Descrição da transação
            - Valor (deve ser positivo)
            - Tipo da transação (Receita ou Despesa)
            - Identificador da pessoa
            - Identificador da categoria

            Regras de negócio:
            - O valor deve ser maior que zero
            - Pessoas menores de 18 anos podem registrar apenas despesas
            - A categoria deve ser compatível com o tipo da transação
            """)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult> Criar(TransacaoCreateDto dto, CancellationToken cancellationToken)
        {
            await _service.Criar(dto, cancellationToken);

            return Ok();
        }
    }
}
