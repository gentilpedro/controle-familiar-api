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
- **Criar/editar/excluir pessoa (sempre cadastro manual, já que a de um membro nasce no `Registrar`)
  exige `EhAdministrador`** (2026-08-12) — mesmo padrão de `FamiliaService.GarantirAdmin`, duplicado
  em `PessoaService` porque ainda não existe um lugar compartilhado pra essa checagem. `GET
  /api/pessoas` continua aberto a qualquer membro (precisa disso pra preencher o select de pessoa ao
  lançar uma transação).
- A FK é **`SetNull`, não `Cascade`**: quando a conta some, as transações continuam valendo para a
  família e a pessoa só vira cadastro comum. Cascade levaria junto histórico que é dos outros.
- `FamiliaService.RemoverMembro` solta o vínculo da pessoa antiga (que fica na família de origem, com
  as transações dela) e cria uma pessoa nova na família individual do removido. São dois
  `SaveChanges` de propósito: o índice único de `UsuarioId` é filtrado, então o vínculo antigo tem
  que estar solto antes de o novo reivindicar o mesmo usuário.

**`BackfillPessoaDosUsuariosExistentes` (2026-08-12) fecha a lacuna das contas antigas.** É migration
só de dados (sem mudança de schema), em duas passadas:

1. Casa por nome: se existe **exatamente uma** `Pessoa` sem dono na família com o mesmo nome do
   `Usuario` (case-insensitive, trim) **e** esse é o único `Usuario` da família pedindo aquele nome,
   vincula os dois. A checagem é 1:1 nos dois sentidos de propósito — só olhar o lado da Pessoa deixa
   passar o caso de duas contas homônimas na mesma família (duas "Ana"), onde o `UPDATE ... FROM`
   ligaria as duas ao mesmo registro de forma não determinística.
2. Quem sobra sem match vira `Pessoa` nova, com idade **18** (mesmo padrão de
   `FamiliaService.RemoverMembro`) — não há como saber a idade real de uma conta que nasceu antes do
   cadastro pedir isso, e assumir adulto evita bloquear receita de alguém que provavelmente não é
   menor.

⚠️ **T-SQL puro, não roda contra o provider de teste.** `CustomWebApplicationFactory` usa
`EnsureCreatedAsync()`, não `Database.Migrate()`, então essa migration nunca é exercida pela suíte —
`UPDATE ... FROM` e `COUNT(*) OVER (PARTITION BY ...)` não têm o mesmo suporte no SQLite dos testes.
Revisão foi manual; qualquer alteração nela merece re-leitura cuidadosa, não só `dotnet test` verde.

`Down()` é vazio de propósito: não há como distinguir depois do fato quais vínculos vieram desta
migration e quais já existiam (ex.: conta criada via `Registrar` entre a migration ser escrita e ser
aplicada em produção). Reverter arriscaria desvincular `Pessoa` legítima — rollback aqui é restaurar
backup, não `Down()`.

## Histórico da família (Relatório Familiar no front)

`GET /api/familia/historico` é um recorte curado de `RegistroAuditoria` — só `CriacaoFamilia`,
`EntradaFamilia`, `RemocaoMembro` e `ExclusaoConta` (`FamiliaService.ObterHistorico`,
`AcoesDoHistorico`). Promoção/rebaixamento de admin também vão pra auditoria, mas ficam de fora: o
endpoint conta quem esteve na família, não a trilha completa de LGPD. Aberto a qualquer membro, não
só admin — é análogo à lista de membros que já é visível em Minha Família.

`RegistroAuditoria.NomeAlvo` (2026-08-12) denormaliza o nome de quem o evento é sobre, porque
`UsuarioId`/`UsuarioAlvoId` podem apontar pra uma linha que não existe mais (o motivo de a entidade
não ter FK, documentado nela) — sem isso, mostrar "Fulano saiu em [data]" depois de uma
`ExclusaoConta` exigiria um JOIN que retorna nada.

⚠️ **`CriacaoFamilia`/`EntradaFamilia` são gravados direto no `_context`, não via `IAuditoriaService`,
dentro de `AuthService.Registrar`.** O contrato de `IAuditoriaService.Registrar` tira `UsuarioId` e
`FamiliaId` de `ICurrentUserService`, que lê claims do JWT da requisição atual — e no meio do
cadastro ainda não existe usuário autenticado, é a própria conta se criando. Usar `_auditoria` ali
derruba o `Registrar` inteiro com `InvalidOperationException: Claim de usuário não encontrada` (foi
exatamente o que aconteceu ao escrever isso — pegue de exemplo antes de "simplificar" essa chamada).

## Transações recorrentes/parceladas (em construção, desde 2026-08-12)

Bloco grande, em vários PRs sequenciais (plano completo salvo em
`C:\Users\pedro.rodrigues\.claude\plans\foamy-knitting-lightning.md` no momento em que isso foi
escrito) — compra parcelada em N meses e salário dividido por percentual em quinzenas. Cada passo
depende do anterior; ver o plano pra a lista completa e a ordem.

**Passo 1 — `Transacao.Data`** (`AdicionaDataNaTransacao`): antes disso, `Transacao` não tinha campo
de data nenhum — `Listar` ordenava por `Id` como proxy de "mais recente". Sem data não tem como uma
parcela cair "em outubro".

- `Data` é `DateOnly`, `NOT NULL`. No `TransacaoCreateDto` é `DateOnly?` (nullable) de propósito —
  mesma razão de `RegistrarDto.Idade`: em tipo não-anulável, omitir o campo cairia silenciosamente
  em `0001-01-01` em vez de dar 400.
- **A migration não teve dado de origem pra copiar** (nunca existiu data antes). Passo em três
  partes: `AddColumn` nullable → `Sql` preenchendo as linhas existentes com **a data do deploy desta
  migration** (`2026-08-12`, hardcoded no `Up()`) → `AlterColumn` pra `NOT NULL`. Não é
  `GETDATE()`/`HasDefaultValueSql` — isso criaria um valor calculado em runtime, ainda mais
  enganoso que uma data fixa registrada. É um artefato conhecido e documentado, não um bug: dado que
  nunca foi capturado não tem como ser reconstruído, só marcado honestamente.
- `Listar` ordena por `Data DESC, Id DESC` (desempate) — não só `Id DESC` como antes. Índice novo
  `(FamiliaId, Data) INCLUDE (Valor, Tipo, CategoriaId, PessoaId)`, cobridor pra essa consulta; não
  conflita com os dois índices existentes (`(FamiliaId,Tipo)`/`(PessoaId,Tipo)`), que servem ao
  `RelatorioService`.

⚠️ **`RelatorioService` continua sem filtro de período** — `Data` existir na transação não implica
relatório por mês. Decisão de escopo explícita, registrada no plano.

**Passo 2 — `PATCH`/`DELETE /transacoes/{id}`**: até aqui só existia criar e listar transação — a
lacuna já era antiga, independente da recorrência. `TransacaoUpdateDto` é parcial (mesmo padrão de
`PessoaUpdateDto`), e a validação (REGRA 1/REGRA 2 de `TransacaoService`) roda sobre o **resultado
final mesclado**, não só sobre o campo enviado — editar só o `Tipo` ainda checa contra a `Pessoa` e
a `Categoria` que a transação já tinha, buscadas de novo. Lógica de validação extraída pra
`ValidarRegrasDeNegocio` (privado, estático), compartilhada por `Criar` e `Atualizar` — mesmo
arquivo, sem introduzir abstração nova.

Não tem nada de série ainda (`SerieId`) — isso é o Passo 3. O contrato de `PATCH`/`DELETE` **vai
mudar** nesse passo seguinte (ganha `AplicarAFuturas`/`excluirFuturas`), registrado no plano.

**Passo 3 — parcelamento (`POST /transacoes/parceladas`)**: `Transacao` ganha `SerieId` (`Guid?`),
`NumeroParcela`/`TotalParcelas` (`int?`) — nulos pra transação avulsa, mesmo valor de `SerieId` em
todas as transações nascidas juntas de um parcelamento (ou, no Passo 4, de uma divisão percentual).
`TotalParcelas` **não é recalculado** se uma ocorrência for excluída — "3/10" pode continuar
mostrando 10 com só 8 restantes, artefato aceito.

- Guid e não um `int` sequencial: um contador central reintroduziria concorrência de escrita (o
  problema que se está evitando não usando isolamento pesado nas séries), e usar o `Id` da primeira
  transação como âncora quebra se justamente ela for excluída sozinha (permitido).
- Divide `ValorTotal` em parcelas iguais (`Math.Round(ValorTotal / N, 2)`), **a última absorve o
  resíduo** do arredondamento — a soma bate sempre, exatamente. Antes de gerar, valida
  `Math.Round(ValorTotal / N, 2) > 0`: sem isso, valor baixo dividido em muitas parcelas gera
  parcela de R$0,00 no meio (achado ao revisar o plano, coberto por
  `CriarParcelada_ComValorMuitoBaixoParaTantasParcelas_Retorna400`).
- Cada parcela em `DataPrimeiraParcela.AddMonths(i)`, **sempre a partir da data original, nunca
  encadeado** (`Data.AddMonths(1).AddMonths(1)...`) — encadear acumularia o efeito de clamping do
  `AddMonths` em dia 29/30/31 (dia que não existe no mês de destino cai no último dia daquele mês;
  comportamento nativo do .NET, aceito).
- `PATCH`/`DELETE` ganharam `AplicarAFuturas`/`excluirFuturas`: propagam a mudança pra
  `NumeroParcela >= a da ocorrência editada`, na mesma série. **`Data` nunca propaga** — só se edita
  na ocorrência individual, mesmo com `AplicarAFuturas=true` (propagar mudaria o espaçamento da
  série). Toda a operação (ocorrência principal + propagadas) roda numa `BeginTransactionAsync`,
  isolamento padrão (`ReadCommitted`) — não há invariante de sistema em jogo aqui, só atomicidade.

⚠️ **Concorrência aceita, não tratada**: duas edições quase simultâneas na mesma série (mais
provável via duplo-clique/duas abas do que dois usuários concorrentes de verdade) podem se sobrepor
sem erro algum, resultado dependendo só da ordem de chegada. Diferente do invariante protegido em
`FamiliaService.RemoverMembro` (nunca ficar sem admin), aqui não há nada do tipo — decisão registrada
de propósito, não omissão.

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
