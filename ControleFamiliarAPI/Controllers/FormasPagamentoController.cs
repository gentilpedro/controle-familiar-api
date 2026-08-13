using ControleFamiliarAPI.DTOs.FormaPagamento;
using ControleFamiliarAPI.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OpenApi;

namespace ControleFamiliarAPI.Controllers
{
    [ApiController]
    [Route("api/formas-pagamento")]
    [Authorize]
    public class FormasPagamentoController : ControllerBase
    {
        private readonly IFormaPagamentoService _service;

        public FormasPagamentoController(IFormaPagamentoService service)
        {
            _service = service;
        }

        [HttpGet]
        [EndpointSummary("Lista as formas de pagamento disponíveis")]
        [EndpointDescription("""
            Retorna as formas de pagamento do sistema (Pix, Dinheiro, Saque)
            junto com as criadas pela família do usuário autenticado.

            Cada item contém:
            - Identificador único
            - Descrição
            - Se é do sistema (não editável nem excluível)
            """)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<ActionResult> Listar(CancellationToken cancellationToken)
        {
            return Ok(await _service.Listar(cancellationToken));
        }

        [HttpPost]
        [EndpointSummary("Cria uma nova forma de pagamento")]
        [EndpointDescription("""
            Registra uma forma de pagamento própria da família — ex.: Cartão de
            crédito, Boleto, Transferência.

            Dados necessários:
            - Descrição (máximo de 100 caracteres)
            """)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult> Criar(FormaPagamentoCreateDto dto, CancellationToken cancellationToken)
        {
            return Ok(await _service.Criar(dto, cancellationToken));
        }

        [HttpPatch("{id}")]
        [EndpointSummary("Atualiza uma forma de pagamento existente")]
        [EndpointDescription("""
            Permite renomear uma forma de pagamento da própria família.
            O identificador vai na rota.

            Importante:
            As formas de pagamento padrão do sistema são compartilhadas por
            todas as famílias e não podem ser editadas.
            """)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult> Atualizar(int id, FormaPagamentoUpdateDto dto, CancellationToken cancellationToken)
        {
            return Ok(await _service.Atualizar(id, dto, cancellationToken));
        }

        [HttpDelete("{id}")]
        [EndpointSummary("Remove uma forma de pagamento")]
        [EndpointDescription("""
            Remove uma forma de pagamento da própria família.

            Importante:
            - As formas de pagamento padrão do sistema não podem ser excluídas
            - Uma forma já usada por alguma transação também não pode ser
              excluída, para não apagar a informação do lançamento
            """)]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult> Deletar(int id, CancellationToken cancellationToken)
        {
            await _service.Deletar(id, cancellationToken);
            return NoContent();
        }
    }
}
