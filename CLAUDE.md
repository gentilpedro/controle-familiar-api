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

## Forma de pagamento (desde 2026-08-12)

`FormaPagamento` é uma entidade nova com o **mesmo desenho de `Categoria`**: catálogo do sistema
(`FamiliaId` nulo — Pix, Dinheiro, Saque, em `Data/FormasPagamentoPadrao.cs`) ao lado das criadas por
cada família, imutáveis para elas (403 em editar/excluir), isoladas entre famílias. `CRUD` em
`/api/formas-pagamento`, espelhando `CategoriasController`/`CategoriaService`.

Ela responde "por onde o dinheiro passou", enquanto a Categoria responde "com o quê" — são eixos
diferentes, por isso entidade nova e não mais uma `FinalidadeCategoria`.

- **`Transacao.FormaPagamentoId` é anulável e continua assim.** As transações que já existiam não
  têm forma de pagamento e não há dado de origem pra inventar uma (mesma lição de
  `AdicionaDataNaTransacao`, que precisou hardcodar uma data no `Up()`). Lançamento sem forma de
  pagamento é válido por design, não um estado a corrigir depois.
- **`TransacaoUpdateDto.RemoverFormaPagamento`** existe porque num PATCH parcial "campo ausente" e
  "campo enviado como null" chegam idênticos em `int?` — sem a flag dava pra trocar a forma de
  pagamento, nunca pra tirá-la. Ela tem precedência sobre `FormaPagamentoId` se os dois vierem.
- **`FormaPagamentoService.Deletar` checa uso antes de apagar.** A FK é `Restrict`: sem a checagem o
  `SaveChanges` estouraria `DbUpdateException` e viraria 500 em vez da explicação. É a diferença
  em relação ao `CategoriaService`, que não faz essa checagem (lacuna conhecida lá, não copiada
  pra cá).
- Vale pra série também: `CriarParcelada`/`CriarRecorrenciaPercentual` aceitam `FormaPagamentoId` e
  aplicam a todas as ocorrências; no `Atualizar`, a forma **propaga** com `AplicarAFuturas` (é
  atributo da compra inteira, diferente de `Data`/`Pago`, que são por ocorrência).
- O seed do sistema está em **dois lugares que precisam ficar sincronizados na mão**:
  `Data/FormasPagamentoPadrao.cs` (fonte do `EnsureCreated` dos testes) e o `Sql` da migration
  `AdicionaFormaPagamento` (fonte de produção) — mesmo par de `CategoriasPadrao` /
  `SeedCategoriasDoSistema`, com o mesmo `NOT EXISTS` que torna a migration segura de reexecutar.

## Filtro de período em `GET /transacoes` (desde 2026-08-12)

`?ano=&mes=` recorta a listagem paginada por mês. **Os dois juntos ou nenhum** — `ano` sozinho
pareceria filtro de ano inteiro e `mes` sozinho não identifica período nenhum, então o par
incompleto é 400 em vez de um recorte silenciosamente diferente do pedido. Sem os dois, a listagem
continua trazendo o histórico inteiro (nenhum cliente antigo quebra).

O filtro é **intervalo** (`Data >= 01/mês && Data < 01/mês+1`), não `t.Data.Year == ano &&
t.Data.Month == mes`: comparar a coluna direto mantém o índice `(FamiliaId, Data)` utilizável,
enquanto `DATEPART` em cima dela forçaria scan. `AddMonths` resolve a virada de ano sozinho
(coberto por `Listar_ComFiltroDeMesEmDezembro_NaoVazaParaJaneiroSeguinte`).

Isso fecha a lacuna registrada no Passo 6: o Painel Mensal do front filtrava no cliente sobre uma
janela de 200 itens, o que **escondia meses inteiros** assim que a família passava de 200
transações no total.

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

**Passo 4 (final) — salário por percentual (`POST /transacoes/recorrencia-percentual`)**: mesmo
mecanismo de série do Passo 3 (`SerieId`/`NumeroParcela`/`TotalParcelas`), reaproveitado pra dividir
um valor total em ocorrências percentuais dentro de um mês — ex.: 35% dia 15, 65% dia 30. Percentuais
**não precisam somar 100** (confirmado com o usuário: adiantamento e saldo podem vir de bases
diferentes).

- **`Categoria.AceitaDivisaoPercentual`** (`bool`, default `false`) trava o fluxo — hoje só a
  categoria de sistema "Salário" tem `true`, marcado em `Data/CategoriasPadrao.cs` (fonte usada por
  `EnsureCreated` nos testes) **e** via `Sql` na migration `AdicionaDivisaoPercentualNaCategoria`
  (fonte usada em produção, mesmo padrão de `SeedCategoriasDoSistema`) — os dois lugares precisam
  ficar sincronizados manualmente, não há automação entre eles. Não é exposto como opção editável
  pra categoria de família: como categoria de sistema é imutável, isso trava o fluxo com segurança,
  **sem comparar nome de categoria em runtime em lugar nenhum** — a única string `"Salário"` do
  código inteiro fica nesses dois pontos de seed, não em `TransacaoService`.
- `TransacaoRecorrenciaPercentualCreateDto.MesReferencia` é `DateOnly?`, mas só ano/mês importam —
  o dia é ignorado, cada `OcorrenciaPercentualDto` tem o próprio `Dia`.
- Dia que não existe no mês de referência (ex.: 31 de fevereiro) **cai no último dia daquele mês** —
  mesma filosofia de clamping do parcelamento, mas aqui é manual: o construtor de `DateOnly` lança
  exceção em vez de clampar (diferente de `AddMonths`), então o código calcula
  `Math.Min(dia, DateTime.DaysInMonth(ano, mes))` antes de montar a data.
- Tipo é sempre `Receita`, implícito — só uma categoria com `AceitaDivisaoPercentual` libera o
  fluxo, e ela é de Receita.

**Passo 5 — status Pago/Recebido**: pedido novo, fora do plano original (compartilhamento de uma
planilha pessoal que motivou este e o próximo passo, "fechamento de mês" — ver plano salvo em
`C:\Users\pedro.rodrigues\.claude\plans\foamy-knitting-lightning.md`, reescrito pra este pedido).
`Transacao.Pago` (`bool`, `NOT NULL`, default `true`) — "paga" pra Despesa, "recebida" pra Receita,
**mesmo campo**, rótulo contextual conforme `Tipo` (não são dois booleanos).

- Migration direta (`AddColumn` com `defaultValue: true`), sem os três passos que `Data` precisou —
  aqui o backfill é uma leitura razoável do que já existe (tudo que já está no banco é passado), não
  uma invenção como foi com data.
- `TransacaoCreateDto.Pago` é `bool?`, mas **não** segue o padrão "omitir vira 400" de `Data`/`Idade`
  — aqui omitir tem um default sensato (`true`, o comum é registrar algo que já aconteceu), não é
  um esquecimento perigoso. `TransacaoUpdateDto.Pago` também é opcional, mesma lógica de `Data`:
  **nunca propaga** com `AplicarAFuturas` (status de pagamento é por ocorrência).
- `CriarParcelada`/`CriarRecorrenciaPercentual`: toda ocorrência nasce com `Pago = false`, sempre,
  sem campo exposto nos DTOs — são obrigações futuras até o usuário confirmar, mesmo a primeira
  parcela (pode ter sido criada hoje mas ainda não paga de fato).
- **`PATCH /transacoes/{id}/pago`** é um endpoint dedicado, separado do `Atualizar` geral — clique
  direto na tabela do front, sem abrir o modal de editar inteiro só pra marcar uma caixinha.
  `TransacaoPagoUpdateDto.Pago` é `bool?` `[Required]`, mesma razão de sempre: em `bool` não-nulo,
  omitir o campo cairia silenciosamente em `false` (desmarcaria a transação sem avisar).

**Passo 6 (final) — Painel Mensal (`GET`/`POST /api/painel-mensal`)**: saldo do mês (receitas
confirmadas − despesas confirmadas, pendência não entra na conta) e um "fechamento" manual que
transporta esse saldo pro mês seguinte como uma transação normal.

- **A recursão resolve sozinha.** O saldo de julho vira uma `Transacao` datada de 01/08 — ao fechar
  agosto, a soma "receitas confirmadas do mês" já inclui essa transação automaticamente, sem lógica
  especial pra "saldo do saldo" (coberto por
  `FecharMes_SaldoTransportadoContaNoFechamentoDoMesSeguinte`).
- **Nova entidade `FechamentoMensal`**, índice único `(FamiliaId, Mes)` — impede fechar o mesmo mês
  duas vezes a nível de banco, não só checando na aplicação. `TransacaoGeradaId` é nullable: saldo
  exatamente zero não gera transação (não faz sentido uma de R$0,00), mas o registro de fechamento
  existe do mesmo jeito — `MesFechado` no resumo não depende de existir uma transação gerada.
- **Nova categoria de sistema `"Saldo Anterior"`** (`Finalidade = Ambas`). `PainelMensalService`
  acha ela **comparando por nome** (`Descricao == "Saldo Anterior" && FamiliaId == null`) — é a
  única exceção ao princípio "nunca comparar categoria por nome" que `AceitaDivisaoPercentual`
  estabeleceu, e a exceção é deliberada: aquele flag protege uma **escolha exposta ao usuário**
  (qual categoria de família libera divisão percentual); aqui é infraestrutura interna do próprio
  seed — o `FamiliaId == null` já isola do catálogo de qualquer família, e o nome nunca muda porque
  categoria de sistema é imutável.
- **A transação gerada é atribuída à `Pessoa` do usuário que fechou o mês** (via `Pessoa.UsuarioId`,
  o vínculo que existe desde o cadastro). Sem essa `Pessoa` (conta antiga, backfill ainda não
  rodado), cai pra qualquer pessoa da família — sempre existe ao menos uma.
- **Não passa pela REGRA 1** (`TransacaoService.ValidarRegrasDeNegocio`, "menor não lança receita")
  — saldo transportado é contabilidade do sistema, não renda de alguém; bloquear o fechamento porque
  o titular é menor de idade seria um bug de UX, não uma proteção que faz sentido aqui.
- `AddMonths` resolve a virada de ano sozinho (dezembro fechado gera a transação em janeiro do ano
  seguinte, não "mês 13" do mesmo ano) — coberto por `FecharMes_EmDezembro_...`.
- ⚠️ **Não existe "reabrir" um mês fechado nesta versão** — decisão de escopo explícita. Se um
  lançamento atrasado entrar no mês depois do fechamento, o saldo transportado fica desatualizado
  até uma correção manual (editar a transação de saldo, ou lançar um ajuste).
- `GET /transacoes` não tinha filtro de período nesta rodada — o Painel Mensal filtrava no cliente
  (buscava até 200 itens e filtrava por `Data` localmente). **Isso mudou**: ver "Filtro de período
  em `GET /transacoes`" abaixo.

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

`dotnet test` — 114 testes, integração com SQLite em memória via `CustomWebApplicationFactory`.
Cobrem autenticação, isolamento por família, health check e as regras de negócio das transações.

⚠️ `CustomWebApplicationFactory.InicializarBancoAsync` insere **os dois catálogos do sistema na
mão** (`CategoriasPadrao.DoSistema()` e `FormasPagamentoPadrao.DoSistema()`): lá o schema vem de
`EnsureCreated` a partir do modelo, sem migration nenhuma. Catálogo de sistema novo precisa ser
adicionado ali também, ou os testes rodam contra uma tabela vazia.
