# ControleFamiliarAPI (FiscalHub) — notas para o Claude

Backend .NET 9 / ASP.NET Core Web API do FiscalHub (`fiscalhub.runasp.net`). EF Core com SQL Server em
produção e SQLite em memória nos testes de integração. Frontend irmão: `controle-familiar-web`
(React/Vite), repositório separado, não um monorepo.

## Padrão de trabalho ("bloco")

Uma mudança de escopo focado = uma branch = um PR = um merge. Sempre verificar com
`dotnet build && dotnet test` antes do merge.

## Acesso: uso livre

O app **não tem cobrança**. Toda conta autenticada acessa Pessoas, Categorias, Transações e
Relatórios; o controle de acesso é só o JWT mais o isolamento por família. Não existe paywall, plano,
trial nem teto de membros por família.

## Assinatura via Stripe — revertida em 2026-08-11

A cobrança chegou a ser implementada (PRs #25 a #29) e foi revertida antes de entrar em uso. Vale
saber por quê, para não reintroduzir o problema:

- A API ficou **fora do ar desde 19/07** com `HTTP 500.30`. O `Program.cs` fazia fail-fast em
  `Stripe:SecretKey`, `Stripe:WebhookSecret`, `Stripe:PriceIndividualId` e `Stripe:PriceFamiliaId`,
  mas os quatro secrets correspondentes nunca foram criados no GitHub. Um secret inexistente vira
  string vazia, o `jq` gravava `""` no `appsettings.Production.json` e a aplicação morria no boot.
- Lição: **fail-fast em configuração opcional derruba o serviço inteiro.** Se a cobrança voltar,
  ou o Stripe entra como opcional (sem chave = assinatura desligada, resto da API no ar), ou os
  secrets são criados no mesmo PR que introduz a validação.

O código está preservado na branch **`backup/assinatura-stripe`**, que aponta para o último commit
com a feature completa (`33280ad`). Lá está toda a implementação: `AssinaturaService`,
`AssinaturaController`, `StripeWebhookController`, `ExigirAssinaturaAttribute`, os enums
`StatusAssinatura`/`TipoPlano` e a migration `AddAssinaturas`.

### Estado do banco de produção

A migration `20260719170623_AddAssinaturas` **continua aplicada** no banco — só o código foi
revertido. As colunas de assinatura seguem em `Usuarios` e `Familias`, ignoradas pelo EF.

Isso é seguro e foi verificado antes do revert: toda coluna adicionada é `nullable: true` ou
`NOT NULL` **com `defaultValue`**, então inserts do código atual usam os defaults do banco. Também
não houve necessidade de migration de reversão, e nenhum dado foi apagado.

Se a feature voltar, a migration vai aparecer como pendente de novo — o `__EFMigrationsHistory`
ainda tem o registro dela, então será preciso conferir antes de deixar o `Database.Migrate()` rodar.

## Pessoa e Usuario: dois conceitos que se cruzam

`Pessoa` é a quem uma transação se atribui. `Usuario` é quem faz login. **Não são a mesma coisa, e
essa é a razão de o CRUD manual de pessoas continuar existindo**: filho pequeno e dependente não têm
conta, e são exatamente quem a regra de "menor de 18 não lança receita" (`TransacaoService`) atende.

Desde 2026-08, `Pessoa.UsuarioId` (nullable) liga uma pessoa à conta que ela representa:

- **`AuthService.Registrar` cria a Pessoa do titular**, dentro da mesma transação que cria a conta —
  nos dois modos, "Nova" e "Entrar". Sem isso, quem se cadastrava caía num painel onde não dava para
  lançar nada, já que toda transação exige uma pessoa. Por isso `RegistrarDto` passou a exigir
  `Idade` (nullable de propósito: em `int` não-nulo, omitir cairia em 0 silenciosamente).
- `UsuarioId` nulo = pessoa cadastrada à mão. `PessoaResponseDto.EhMembro` é o que o front usa para
  marcar quem é membro e não oferecer exclusão.
- **`PessoaService.Deletar` recusa pessoa vinculada** — excluí-la deixaria um membro ativo sem
  ninguém para lançar despesa. Ela só sai junto com a conta.
- A FK é **`SetNull`, não `Cascade`**: quando a conta some, as transações continuam valendo para a
  família e a pessoa só vira cadastro comum. Cascade levaria junto histórico que é dos outros.
- `FamiliaService.RemoverMembro` solta o vínculo da pessoa antiga (que fica na família de origem, com
  as transações dela) e cria uma pessoa nova na família individual do removido. São dois
  `SaveChanges` de propósito: o índice único de `UsuarioId` é filtrado, então o vínculo antigo tem
  que estar solto antes de o novo reivindicar o mesmo usuário.

⚠️ **A migration não faz backfill.** Conta criada antes disso continua sem `Pessoa` vinculada — o que
está certo, porque essas famílias já cadastraram as pessoas delas à mão e um backfill duplicaria. O
efeito prático é que `EhMembro` vem `false` para todo mundo que já existia, e a proteção contra
exclusão só passa a valer para conta nova.

## Deploy e release

`.github/workflows/deploy-monsterasp.yml` roda em push na `main`: build → test → publish → injeta
secrets no `appsettings.Production.json` via `jq` → `app_offline.htm` por FTP → `FTP-Deploy-Action` →
volta online. Um job `release` com `needs: build_and_deploy` cria a tag e a Release só depois de o
deploy passar (patch incrementado a partir da maior tag `vN.N.N`).

As migrations **não** rodam no CI: o MSSQL free do MonsterASP.NET só aceita "Local access", que um
runner do GitHub não alcança. A aplicação aplica sozinha ao subir (`Database.Migrate()` no
`Program.cs`).

Secrets necessários: `FTP_SERVER`, `FTP_USERNAME`, `FTP_PASSWORD`, `DB_CONNECTION_STRING`, `JWT_KEY`,
`WEB_ORIGIN`, `SCALAR_USERNAME`, `SCALAR_PASSWORD`, e os `SMTP_*` (opcionais — sem eles o convite por
e-mail fica desativado e o código de convite continua funcionando).

## Testes

`dotnet test` — 53 testes, integração com SQLite em memória via `CustomWebApplicationFactory`.
Cobrem autenticação, isolamento por família, health check e as regras de negócio das transações.
