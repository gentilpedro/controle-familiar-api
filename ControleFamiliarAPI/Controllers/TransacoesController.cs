using ControleFamiliarAPI.DTOs.Transacao;
using ControleFamiliarAPI.Responses;
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

        [HttpPatch("{id}")]
        [Tags("Transações")]
        [EndpointSummary("Atualiza uma transação financeira")]
        [EndpointDescription("""
            Permite alterar qualquer campo de uma transação já cadastrada.
            O identificador vai na rota; o corpo só precisa trazer os campos
            que mudam.

            As mesmas regras de negócio da criação valem aqui, aplicadas ao
            resultado final — ex.: se só o Tipo mudar, a compatibilidade com
            a Pessoa e a Categoria que a transação já tinha é checada de
            novo.
            """)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult> Atualizar(int id, TransacaoUpdateDto dto, CancellationToken cancellationToken)
        {
            await _service.Atualizar(id, dto, cancellationToken);
            return Ok(new ApiResponse<string>("Transação atualizada com sucesso"));
        }

        [HttpDelete("{id}")]
        [Tags("Transações")]
        [EndpointSummary("Remove uma transação financeira")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult> Deletar(int id, CancellationToken cancellationToken)
        {
            await _service.Deletar(id, cancellationToken);
            return Ok(new ApiResponse<string>("Transação removida com sucesso"));
        }
    }
}
