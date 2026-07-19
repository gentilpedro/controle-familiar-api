using ControleFamiliarAPI.DTOs.Assinatura;
using ControleFamiliarAPI.Responses;
using ControleFamiliarAPI.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OpenApi;

namespace ControleFamiliarAPI.Controllers
{
    [ApiController]
    [Route("api/assinatura")]
    [Authorize]
    public class AssinaturaController : ControllerBase
    {
        private readonly IAssinaturaService _service;

        public AssinaturaController(IAssinaturaService service)
        {
            _service = service;
        }

        [HttpPost("checkout")]
        [Tags("Assinatura")]
        [EndpointSummary("Cria uma sessão de Checkout do Stripe para assinar um plano")]
        [EndpointDescription("""
            Cria uma Checkout Session hospedada pelo Stripe para o plano
            informado (Individual ou Família) e devolve a URL para
            redirecionar o usuário.

            O plano Individual concede um período de teste grátis na primeira
            vez que o usuário assina - depois disso, ou no plano Família (que
            não tem teste grátis), o cartão é cobrado imediatamente ao
            concluir o checkout.
            """)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<ActionResult> CriarCheckout(CriarCheckoutDto dto, CancellationToken cancellationToken)
        {
            var resultado = await _service.CriarCheckoutSession(dto.TipoPlano, cancellationToken);
            return Ok(new ApiResponse<CheckoutResponseDto>(resultado));
        }

        [HttpGet("status")]
        [Tags("Assinatura")]
        [EndpointSummary("Retorna o status da assinatura do usuário autenticado")]
        [EndpointDescription("""
            TemAcesso é true se a família do usuário tiver o plano Família
            ativo, ou se o próprio usuário tiver o plano Individual ativo ou
            em período de teste.
            """)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<ActionResult> ObterStatus(CancellationToken cancellationToken)
        {
            var resultado = await _service.ObterStatus(cancellationToken);
            return Ok(new ApiResponse<AssinaturaStatusDto>(resultado));
        }

        [HttpPost("portal")]
        [Tags("Assinatura")]
        [EndpointSummary("Cria uma sessão do Customer Portal do Stripe")]
        [EndpointDescription("""
            Cria uma sessão do Customer Portal hospedado pelo Stripe, onde o
            usuário pode trocar forma de pagamento, ver faturas e cancelar a
            assinatura. Devolve a URL para redirecionar o usuário.
            """)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<ActionResult> CriarPortal(CancellationToken cancellationToken)
        {
            var resultado = await _service.CriarPortalSession(cancellationToken);
            return Ok(new ApiResponse<PortalResponseDto>(resultado));
        }
    }
}
