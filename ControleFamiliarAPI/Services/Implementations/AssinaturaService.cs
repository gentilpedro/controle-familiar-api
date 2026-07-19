using ControleFamiliarAPI.Data;
using ControleFamiliarAPI.DTOs.Assinatura;
using ControleFamiliarAPI.Exceptions;
using ControleFamiliarAPI.Models;
using ControleFamiliarAPI.Models.Enums;
using ControleFamiliarAPI.Services.Interfaces;
using Microsoft.AspNetCore.Identity;
using Stripe;
using Stripe.Checkout;

namespace ControleFamiliarAPI.Services.Implementations
{
    public class AssinaturaService : IAssinaturaService
    {
        private readonly AppDbContext _context;
        private readonly UserManager<Usuario> _userManager;
        private readonly ICurrentUserService _currentUser;
        private readonly IConfiguration _configuration;

        public AssinaturaService(
            AppDbContext context,
            UserManager<Usuario> userManager,
            ICurrentUserService currentUser,
            IConfiguration configuration)
        {
            _context = context;
            _userManager = userManager;
            _currentUser = currentUser;
            _configuration = configuration;
        }

        public async Task<CheckoutResponseDto> CriarCheckoutSession(TipoPlano tipoPlano, CancellationToken cancellationToken = default)
        {
            var usuario = await _userManager.FindByIdAsync(_currentUser.UsuarioId.ToString())
                ?? throw new UnauthorizedException("Usuário não encontrado.");

            var familia = await _context.Familias.FindAsync(new object?[] { usuario.FamiliaId }, cancellationToken)
                ?? throw new Exception("Família não encontrada.");

            var priceId = tipoPlano == TipoPlano.Individual
                ? _configuration["Stripe:PriceIndividualId"]!
                : _configuration["Stripe:PriceFamiliaId"]!;

            var customerId = await ObterOuCriarCustomerAsync(tipoPlano, usuario, familia, cancellationToken);

            var frontendUrl = _configuration["Frontend:BaseUrl"]?.TrimEnd('/') ?? "http://localhost:5173";

            var options = new SessionCreateOptions
            {
                Customer = customerId,
                Mode = "subscription",
                LineItems = new List<SessionLineItemOptions>
                {
                    new() { Price = priceId, Quantity = 1 }
                },
                SuccessUrl = $"{frontendUrl}/painel/assinatura?sucesso=1",
                CancelUrl = $"{frontendUrl}/painel/assinatura?cancelado=1",
                ClientReferenceId = tipoPlano == TipoPlano.Individual
                    ? usuario.Id.ToString()
                    : familia.Id.ToString(),
                SubscriptionData = new SessionSubscriptionDataOptions
                {
                    Metadata = new Dictionary<string, string>
                    {
                        ["UsuarioId"] = usuario.Id.ToString(),
                        ["FamiliaId"] = familia.Id.ToString(),
                        ["TipoPlano"] = tipoPlano.ToString()
                    }
                }
            };

            // Trial só existe no plano Individual, e só na primeira vez que o
            // usuário assina. TrialIndividualUsado é marcado a partir do
            // webhook (Bloco 3), não aqui, pra refletir o que o Stripe de
            // fato aplicou - não o que a gente pediu pra aplicar.
            if (tipoPlano == TipoPlano.Individual && !usuario.TrialIndividualUsado)
            {
                var trialDias = _configuration.GetValue<int?>("Stripe:TrialDiasIndividual") ?? 7;
                options.SubscriptionData.TrialPeriodDays = trialDias;
            }

            var sessionService = new SessionService();
            var session = await sessionService.CreateAsync(options, cancellationToken: cancellationToken);

            return new CheckoutResponseDto { Url = session.Url };
        }

        public async Task<AssinaturaStatusDto> ObterStatus(CancellationToken cancellationToken = default)
        {
            var usuario = await _userManager.FindByIdAsync(_currentUser.UsuarioId.ToString())
                ?? throw new UnauthorizedException("Usuário não encontrado.");

            var familia = await _context.Familias.FindAsync(new object?[] { usuario.FamiliaId }, cancellationToken)
                ?? throw new Exception("Família não encontrada.");

            var individualCobreAcesso = usuario.StatusAssinaturaIndividual is StatusAssinatura.Ativa or StatusAssinatura.EmTeste;
            var familiaCobreAcesso = familia.StatusAssinaturaFamilia == StatusAssinatura.Ativa;

            return new AssinaturaStatusDto
            {
                TemAcesso = individualCobreAcesso || familiaCobreAcesso,
                StatusIndividual = usuario.StatusAssinaturaIndividual.ToString(),
                StatusFamilia = familia.StatusAssinaturaFamilia.ToString(),
                TrialIndividualUsado = usuario.TrialIndividualUsado,
                AssinaturaIndividualValidaAte = usuario.AssinaturaIndividualValidaAte,
                AssinaturaFamiliaValidaAte = familia.AssinaturaFamiliaValidaAte
            };
        }

        public async Task<PortalResponseDto> CriarPortalSession(CancellationToken cancellationToken = default)
        {
            var usuario = await _userManager.FindByIdAsync(_currentUser.UsuarioId.ToString())
                ?? throw new UnauthorizedException("Usuário não encontrado.");

            var familia = await _context.Familias.FindAsync(new object?[] { usuario.FamiliaId }, cancellationToken)
                ?? throw new Exception("Família não encontrada.");

            // O portal é aberto no Customer do próprio usuário (assinatura
            // Individual) se existir; senão cai pro Customer da família
            // (quem pagou o plano Família, normalmente o administrador).
            var customerId = usuario.StripeCustomerId ?? familia.StripeCustomerId
                ?? throw new BusinessRuleException("Nenhuma assinatura encontrada para gerenciar.");

            var frontendUrl = _configuration["Frontend:BaseUrl"]?.TrimEnd('/') ?? "http://localhost:5173";

            var portalService = new Stripe.BillingPortal.SessionService();
            var portalSession = await portalService.CreateAsync(new Stripe.BillingPortal.SessionCreateOptions
            {
                Customer = customerId,
                ReturnUrl = $"{frontendUrl}/painel/assinatura"
            }, cancellationToken: cancellationToken);

            return new PortalResponseDto { Url = portalSession.Url };
        }

        public async Task ProcessarWebhookAsync(Event stripeEvent, CancellationToken cancellationToken = default)
        {
            switch (stripeEvent.Type)
            {
                case "checkout.session.completed":
                {
                    var session = stripeEvent.Data.Object as Session;
                    if (!string.IsNullOrWhiteSpace(session?.SubscriptionId))
                        await SincronizarAssinaturaAsync(session.SubscriptionId, cancellationToken);
                    break;
                }

                case "invoice.paid":
                case "invoice.payment_failed":
                {
                    var invoice = stripeEvent.Data.Object as Invoice;
                    var subscriptionId = invoice?.Parent?.SubscriptionDetails?.SubscriptionId;
                    if (!string.IsNullOrWhiteSpace(subscriptionId))
                        await SincronizarAssinaturaAsync(subscriptionId, cancellationToken);
                    break;
                }

                case "customer.subscription.updated":
                case "customer.subscription.deleted":
                {
                    var subscription = stripeEvent.Data.Object as Subscription;
                    if (!string.IsNullOrWhiteSpace(subscription?.Id))
                        await SincronizarAssinaturaAsync(subscription.Id, cancellationToken);
                    break;
                }

                // Eventos que não afetam o status da assinatura (ex.: outros
                // tipos que o endpoint do Dashboard venha a mandar) são
                // reconhecidos com 200 OK sem nenhuma ação - ver
                // StripeWebhookController.
                default:
                    break;
            }
        }

        // Busca a Subscription atual no Stripe e sincroniza o estado local
        // (Usuario ou Familia, conforme a metadata gravada na criação do
        // Checkout) - é o único lugar que escreve Status*/StripeSubscriptionId*/
        // *ValidaAte/TrialIndividualUsado, sempre a partir do que o Stripe
        // efetivamente aplicou.
        private async Task SincronizarAssinaturaAsync(string subscriptionId, CancellationToken cancellationToken)
        {
            var subscriptionService = new Stripe.SubscriptionService();
            var subscription = await subscriptionService.GetAsync(subscriptionId, cancellationToken: cancellationToken);

            if (!subscription.Metadata.TryGetValue("TipoPlano", out var tipoPlanoTexto)
                || !Enum.TryParse<TipoPlano>(tipoPlanoTexto, out var tipoPlano))
                return;

            var status = MapearStatus(subscription.Status);
            var validaAte = subscription.Items?.Data?.Count > 0
                ? subscription.Items.Data[0].CurrentPeriodEnd
                : (DateTime?)null;

            if (tipoPlano == TipoPlano.Individual)
            {
                if (!subscription.Metadata.TryGetValue("UsuarioId", out var usuarioIdTexto))
                    return;

                var usuario = await _userManager.FindByIdAsync(usuarioIdTexto);
                if (usuario == null)
                    return;

                usuario.StripeSubscriptionIdIndividual = subscription.Id;
                usuario.StatusAssinaturaIndividual = status;
                usuario.AssinaturaIndividualValidaAte = validaAte;

                if (subscription.TrialStart.HasValue)
                    usuario.TrialIndividualUsado = true;

                await _userManager.UpdateAsync(usuario);
            }
            else
            {
                if (!subscription.Metadata.TryGetValue("FamiliaId", out var familiaIdTexto)
                    || !int.TryParse(familiaIdTexto, out var familiaId))
                    return;

                var familia = await _context.Familias.FindAsync(new object?[] { familiaId }, cancellationToken);
                if (familia == null)
                    return;

                familia.StripeSubscriptionIdFamilia = subscription.Id;
                familia.StatusAssinaturaFamilia = status;
                familia.AssinaturaFamiliaValidaAte = validaAte;

                await _context.SaveChangesAsync(cancellationToken);
            }
        }

        // Mapeamento direto dos status de Subscription do Stripe pro nosso
        // enum interno. "past_due"/"unpaid"/"incomplete" contam como
        // Inadimplente (o acesso é revogado, mas a assinatura ainda pode se
        // recuperar via Smart Retries do Stripe); "canceled"/
        // "incomplete_expired" são estados finais.
        private static StatusAssinatura MapearStatus(string stripeStatus) => stripeStatus switch
        {
            "trialing" => StatusAssinatura.EmTeste,
            "active" => StatusAssinatura.Ativa,
            "past_due" or "unpaid" or "incomplete" or "paused" => StatusAssinatura.Inadimplente,
            "canceled" or "incomplete_expired" => StatusAssinatura.Cancelada,
            _ => StatusAssinatura.Nenhuma
        };

        // Reaproveita o Customer do Stripe já existente (Usuario para o plano
        // Individual, Familia para o plano Família) ou cria um novo na
        // primeira assinatura.
        private async Task<string> ObterOuCriarCustomerAsync(TipoPlano tipoPlano, Usuario usuario, Familia familia, CancellationToken cancellationToken)
        {
            var customerIdExistente = tipoPlano == TipoPlano.Individual
                ? usuario.StripeCustomerId
                : familia.StripeCustomerId;

            if (!string.IsNullOrWhiteSpace(customerIdExistente))
                return customerIdExistente;

            var customerService = new Stripe.CustomerService();
            var customer = await customerService.CreateAsync(new Stripe.CustomerCreateOptions
            {
                Email = usuario.Email,
                Name = tipoPlano == TipoPlano.Individual ? usuario.Nome : familia.Nome
            }, cancellationToken: cancellationToken);

            if (tipoPlano == TipoPlano.Individual)
            {
                usuario.StripeCustomerId = customer.Id;
                await _userManager.UpdateAsync(usuario);
            }
            else
            {
                familia.StripeCustomerId = customer.Id;
                await _context.SaveChangesAsync(cancellationToken);
            }

            return customer.Id;
        }
    }
}
