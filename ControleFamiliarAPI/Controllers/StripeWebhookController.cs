using ControleFamiliarAPI.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OpenApi;
using Stripe;

namespace ControleFamiliarAPI.Controllers
{
    [ApiController]
    [Route("api/webhooks/stripe")]
    [AllowAnonymous]
    public class StripeWebhookController : ControllerBase
    {
        private readonly IAssinaturaService _assinaturaService;
        private readonly IConfiguration _configuration;
        private readonly ILogger<StripeWebhookController> _logger;

        public StripeWebhookController(
            IAssinaturaService assinaturaService,
            IConfiguration configuration,
            ILogger<StripeWebhookController> logger)
        {
            _assinaturaService = assinaturaService;
            _configuration = configuration;
            _logger = logger;
        }

        [HttpPost]
        [Tags("Assinatura")]
        [EndpointSummary("Endpoint de webhook do Stripe (uso exclusivo do Stripe)")]
        [EndpointDescription("""
            Recebe eventos de assinatura do Stripe (checkout concluído, fatura
            paga/recusada, assinatura atualizada/cancelada) e sincroniza o
            status local. A assinatura da requisição é validada pelo header
            Stripe-Signature - não é um endpoint autenticado por JWT, mas só
            aceita requisições assinadas pelo Stripe.
            """)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> Handle(CancellationToken cancellationToken)
        {
            var webhookSecret = _configuration["Stripe:WebhookSecret"]!;

            using var reader = new StreamReader(Request.Body);
            var json = await reader.ReadToEndAsync(cancellationToken);

            Event stripeEvent;
            try
            {
                // throwOnApiVersionMismatch: false - o SDK trava numa versão
                // fixa da API do Stripe (é uma lib fortemente tipada), mas a
                // versão configurada na conta Stripe (Dashboard) não é
                // sincronizada com isso automaticamente. Validar só a
                // assinatura (HMAC) e não a versão evita rejeitar eventos
                // legítimos por causa de um mismatch de versão que não tem
                // relação com a integridade do evento.
                stripeEvent = EventUtility.ConstructEvent(
                    json,
                    Request.Headers["Stripe-Signature"],
                    webhookSecret,
                    throwOnApiVersionMismatch: false);
            }
            catch (StripeException ex)
            {
                _logger.LogWarning(ex, "Falha ao validar assinatura de webhook do Stripe.");
                return BadRequest();
            }

            await _assinaturaService.ProcessarWebhookAsync(stripeEvent, cancellationToken);

            return Ok();
        }
    }
}
