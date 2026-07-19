# ControleFamiliarAPI (FiscalHub) — notas para o Claude

Backend .NET 9 / ASP.NET Core Web API do FiscalHub (`fiscalhub.runasp.net`). EF Core com SQL Server em
produção e SQLite em memória nos testes de integração. Frontend irmão: `controle-familiar-web`
(React/Vite), repositório separado, não um monorepo.

## Padrão de trabalho ("bloco")

Uma mudança de escopo focado = uma branch = um PR = um merge. Sempre verificar com
`dotnet build && dotnet test` antes do merge. O agente (Claude) normalmente não tem `dotnet`, `gh` nem
credenciais de push disponíveis no seu sandbox — cada bloco é implementado e commitado localmente, e o
build/teste/push/PR são delegados ao usuário com os comandos exatos.

## Assinatura paga via Stripe (implementada em 2026-07)

O app deixou de ser gratuito: acesso aos dados financeiros (Pessoas, Categorias, Transações,
Relatórios) agora exige assinatura mensal ativa via Stripe.

### Regras de negócio

- **Dois planos mensais, preço fixo** (sem tiers/por-assento): **Individual** (mais barato, libera só
  quem assina) e **Família** (mais caro, libera todos os membros da família enquanto ativo — teto de
  **5 pessoas por família**, validado em `AuthService.EntrarEmFamilia`).
- **Trial de 7 dias, só no plano Individual.** Contado por usuário (`Usuario.TrialIndividualUsado`),
  não por data de cadastro da conta — mesmo quem já tinha família antes e migra pro Individual tem
  direito ao trial na primeira vez que assina.
- **Controle de acesso em tempo real** (HTTP 402), sem migração de dados quando o pagamento atrasa.
  Sair da família continua sendo só via remoção/exclusão — sem relação com pagamento.
- **Grandfathering das contas 1-5** (as contas reais de produção existentes antes da feature, todas do
  próprio usuário/dono do projeto): marcadas como assinantes ativos direto via `migrationBuilder.Sql`
  na migration `AddAssinaturas`, sem passar pelo Stripe.
- **Stripe 100% hospedado**: Checkout Session, Customer Portal e Smart Retries — sem UI de pagamento
  customizada.

### Arquitetura (nesta API)

- `Models/Enums/StatusAssinatura.cs` (`Nenhuma, EmTeste, Ativa, Inadimplente, Cancelada`) e
  `Models/Enums/TipoPlano.cs` (`Individual = 1, Familia = 2`).
- `Usuario`: `StripeCustomerId`, `StripeSubscriptionIdIndividual`, `StatusAssinaturaIndividual`,
  `AssinaturaIndividualValidaAte`, `TrialIndividualUsado`.
- `Familia`: `StripeCustomerId`, `StripeSubscriptionIdFamilia`, `StatusAssinaturaFamilia`,
  `AssinaturaFamiliaValidaAte`.
- `Services/Implementations/AssinaturaService.cs` — toda a lógica: `CriarCheckoutSession`,
  `ObterStatus`, `CriarPortalSession`, `ProcessarWebhookAsync` (trata `checkout.session.completed`,
  `invoice.paid`, `invoice.payment_failed`, `customer.subscription.updated`,
  `customer.subscription.deleted`). É o único lugar que escreve `Status*`/`StripeSubscriptionId*`/
  `*ValidaAte`/`TrialIndividualUsado` — sempre buscando a Subscription atual no Stripe antes de gravar,
  nunca confiando cegamente no payload do evento.
- `Controllers/AssinaturaController.cs` (`POST /api/assinatura/checkout`, `GET /api/assinatura/status`,
  `POST /api/assinatura/portal`, `[Authorize]`) e `Controllers/StripeWebhookController.cs`
  (`POST /api/webhooks/stripe`, `[AllowAnonymous]`, valida assinatura HMAC via `Stripe-Signature`).
- `Filters/ExigirAssinaturaAttribute.cs` — `IAsyncActionFilter` que devolve 402 se `!TemAcesso`.
  Aplicado só em `PessoasController`, `CategoriasController`, `TransacoesController`,
  `RelatoriosController` — **não** em Auth/Familia/Assinatura, pra quem não pagou conseguir gerenciar a
  conta e ir assinar.
- `Exceptions/PagamentoRequeridoException.cs`, mapeada pelo `ErrorMiddleware` pro HTTP 402, mesmo
  padrão das outras exceções de domínio (`BusinessRuleException`, `ForbiddenException`, etc.).
- Config: `Stripe:SecretKey`, `Stripe:WebhookSecret`, `Stripe:PriceIndividualId`,
  `Stripe:PriceFamiliaId`, `Stripe:TrialDiasIndividual` — fail-fast no `Program.cs`, mesmo padrão do
  `Jwt:Key`/`ConnectionStrings:DefaultConnection`. **O código nunca referencia valores em reais** — só
  Price IDs. Preço é decisão livre no Dashboard do Stripe, sem precisar mudar código.

### Gotchas do Stripe.net (SDK 52.1.1) verificados na fonte oficial

A API do Stripe reestruturou vários campos nas versões recentes ("Basil"); a doc/treino pode estar
desatualizada. Confirmado direto no código-fonte do `stripe-dotnet` (GitHub) antes de escrever:

- `Invoice` **não tem** campo `Subscription`/`SubscriptionId` direto na raiz. É
  `Invoice.Parent.SubscriptionDetails.SubscriptionId` (string, sempre disponível) — existe também
  `.Subscription` (objeto, só populado se expandido explicitamente, evitar).
- `Subscription` **não tem** `CurrentPeriodEnd` na raiz — moveu pra
  `Subscription.Items.Data[0].CurrentPeriodEnd` (por item, suporta preços com intervalos diferentes no
  mesmo Subscription).
- `Checkout.Session.SubscriptionId` continua um campo direto (não foi restruturado) — confirmado que
  fica populado no evento `checkout.session.completed`.
- `EventUtility.ConstructEvent(..., throwOnApiVersionMismatch: false)` — passado explicitamente como
  `false`. O SDK trava numa versão fixa da API (lib fortemente tipada), mas a versão configurada na
  conta Stripe (Dashboard) não é sincronizada automaticamente com isso. Sem esse `false`, um mismatch
  de versão rejeitaria webhooks legítimos com erro de assinatura, mesmo a assinatura HMAC estando
  correta.

### Testes

`ControleFamiliarAPI.Tests/Infrastructure/AuthTestHelper.cs` — `RegistrarNovaFamiliaAsync` marca a
assinatura Individual do usuário recém-criado como `Ativa` direto no banco (via `CustomWebApplicationFactory`),
porque toda conta nova nasce sem assinatura e isso quebraria qualquer teste de integração que bata em
rotas financeiras. `AssinaturaPaywallTests.cs` cobre especificamente o 402 sem assinatura e a rejeição
do 6º membro da família.

### Pendências pós-deploy (fora do código, ver memória "Stripe go-live checklist")

1. Criar os Products/Prices em modo **live** no Stripe (hoje só existem em teste) e trocar os 4 secrets
   do GitHub Actions (`STRIPE_SECRET_KEY`, `STRIPE_WEBHOOK_SECRET`, `STRIPE_PRICE_INDIVIDUAL_ID`,
   `STRIPE_PRICE_FAMILIA_ID`) pelos valores live.
2. Configurar o endpoint de webhook no Dashboard (`Developers > Webhooks`) apontando pra
   `https://fiscalhub.runasp.net/api/webhooks/stripe` e copiar o Signing Secret live.
3. Habilitar o Customer Portal em `Settings > Billing > Customer Portal` (senão
   `POST /assinatura/portal` falha).
