# 💰 Controle Financeiro API

API desenvolvida em **.NET 9** para gerenciamento de controle financeiro, permitindo cadastro de pessoas, categorias, transações e geração de relatórios — de forma individual ou compartilhada entre várias pessoas de uma mesma família.

Este projeto foi desenvolvido como **teste técnico**, com foco em:

* Organização de código
* Regras de negócio
* Estrutura em camadas
* Documentação com OpenAPI (Scalar)

---

# 🚀 Tecnologias utilizadas

* [.NET 9](https://dotnet.microsoft.com/)
* ASP.NET Core Web API
* Entity Framework Core
* SQL Server
* ASP.NET Core Identity + JWT
* Scalar (OpenAPI UI moderna)
* Docker (SQL Server local para desenvolvimento)

---

# 📦 Como rodar o projeto

## 1️⃣ Subir o banco de dados (SQL Server via Docker)

```bash
docker compose up -d
```

Isso sobe um container SQL Server na porta `14330`. A senha do usuário `sa` vem do arquivo `.env` (veja `.env.example` — copie para `.env` e ajuste a senha antes de subir o container).

## 2️⃣ Configurar os segredos locais

A aplicação **não** roda com segredos hardcoded no `appsettings.json` — em desenvolvimento eles vêm do [User Secrets](https://learn.microsoft.com/aspnet/core/security/app-secrets) do .NET, que ficam fora do repositório:

```bash
cd ControleFamiliarAPI

dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=localhost,14330;Database=ControleFinanceiro;User Id=sa;Password=<sua-senha-do-.env>;TrustServerCertificate=True;"

dotnet user-secrets set "Jwt:Key" "<uma-chave-longa-e-aleatoria>"
```

Se `ConnectionStrings:DefaultConnection` ou `Jwt:Key` não estiverem configurados, a aplicação falha no startup com uma mensagem explicando o que fazer — isso é intencional, para nunca subir silenciosamente sem segredo configurado.

## 3️⃣ Restaurar dependências

```bash
dotnet restore
```

## 4️⃣ Executar a aplicação

```bash
dotnet run
```

As migrations pendentes são aplicadas automaticamente quando a aplicação sobe (`Database.Migrate()` no `Program.cs`) — não precisa rodar `dotnet ef database update` manualmente, nem em dev nem em produção.

## 5️⃣ Acessar a documentação

Após rodar o projeto, acesse:

```bash
https://localhost:{porta}/scalar/v1
```

Ou:

```bash
https://localhost:{porta}/scalar
```

---

# 🔐 Autenticação

A API usa **ASP.NET Core Identity + JWT**. Todo usuário pertence a uma **Família**:

* Ao se cadastrar, o usuário pode **criar uma família nova** (uso individual — ele é o único membro) ou **entrar em uma família existente** informando o código de convite de outro membro.
* Todos os dados (Pessoas, Categorias, Transações) são isolados por família: usuários de famílias diferentes não veem os dados uns dos outros. Usuários da mesma família compartilham os mesmos dados.
* Todas as rotas de Pessoas, Categorias, Transações e Relatórios exigem um token JWT válido (`Authorization: Bearer <token>`).

### Endpoints de autenticação

```
POST /api/auth/registrar   → cria a conta (nova família ou entrar via código de convite)
POST /api/auth/login       → autentica e retorna o token JWT
GET  /api/auth/me          → dados do usuário logado e da sua família (inclui o código de convite)
```

---

# 👨‍👩‍👧 Gestão da família

Quem cria a família (`modoFamilia: "Nova"` no cadastro) já nasce **administrador**; quem entra por código de convite entra como membro comum. Só administradores podem gerenciar a família.

```
GET  /api/familia                              → dados da família (nome, código, membros)
DELETE /api/familia/membros/{usuarioId}         → remove um membro (admin)
POST /api/familia/membros/{usuarioId}/promover  → torna o membro administrador (admin)
POST /api/familia/membros/{usuarioId}/rebaixar  → tira o status de admin do membro (admin)
POST /api/familia/regenerar-codigo              → invalida o código atual e gera outro (admin)
POST /api/familia/convidar                      → envia um e-mail de convite com o código (admin)
```

Regras:

* Não é possível remover a si mesmo, nem rebaixar/remover o último administrador da família — a família sempre precisa ter pelo menos um admin.
* Ao remover um membro, ele não fica sem conta: ganha automaticamente uma família nova e individual, da qual passa a ser o único membro e administrador.
* `POST /api/familia/convidar` exige SMTP configurado (veja a seção de deploy); sem isso, retorna erro explicando para compartilhar o código manualmente.

---

# 📚 Funcionalidades

## 👤 Pessoas

* Criar pessoa
* Listar pessoas
* Atualizar pessoa
* Remover pessoa

## 🏷️ Categorias

* Criar categoria
* Listar categorias
* Remover categoria

Finalidades possíveis:

* Receita
* Despesa
* Ambas

## 💰 Transações

* Criar transação
* Listar transações

### Regras de negócio:

* Valor deve ser positivo
* Menores de idade (< 18 anos) só podem ter despesas
* Categoria deve ser compatível com o tipo da transação
* Pessoa e Categoria informadas devem pertencer à família do usuário autenticado

## 📊 Relatórios

### Totais por pessoa

Retorna:

* Total de receitas
* Total de despesas
* Saldo (receita - despesa)

### Totais por categoria (opcional)

Retorna:

* Total agrupado por categoria

---

# 🧱 Estrutura do projeto

```bash
Controllers/
Services/
DTO/
Models/
Data/
Exceptions/
Middlewares/
```

---

# 🔎 Exemplos de requisição

## Criar conta (uso individual)

```json
POST /api/auth/registrar

{
  "nome": "Pedro",
  "email": "pedro@exemplo.com",
  "senha": "senha123",
  "modoFamilia": "Nova",
  "nomeFamilia": "Família do Pedro"
}
```

## Entrar em uma família existente

```json
POST /api/auth/registrar

{
  "nome": "Ana",
  "email": "ana@exemplo.com",
  "senha": "senha123",
  "modoFamilia": "Entrar",
  "codigoConvite": "06189F49"
}
```

## Criar pessoa

```json
POST /api/pessoas
Authorization: Bearer <token>

{
  "nome": "Pedro",
  "idade": 30
}
```

## Criar categoria

```json
POST /api/categorias
Authorization: Bearer <token>

{
  "descricao": "Salário",
  "finalidade": "Receita"
}
```

## Criar transação

```json
POST /api/transacoes
Authorization: Bearer <token>

{
  "descricao": "Pagamento",
  "valor": 1500,
  "tipo": "Receita",
  "pessoaId": 1,
  "categoriaId": 1
}
```

---

# 📖 Documentação da API

A API está documentada utilizando **Scalar**, que consome o padrão OpenAPI.

A documentação inclui:

* Descrição dos endpoints
* Exemplos de requisição
* Regras de negócio
* Tipos de resposta

---

# 🚀 Deploy (CI/CD) para o MonsterASP.NET

O workflow `.github/workflows/deploy-monsterasp.yml` builda, publica e faz deploy da API para o [MonsterASP.NET](https://monsterasp.net/) via FTP — no mesmo padrão usado em outros projetos .NET hospedados lá.

### Quando roda

* Automaticamente em todo push na branch `main`.
* Manualmente, a qualquer momento, pelo botão **Run workflow** na aba *Actions* do GitHub.

Branches de feature e Pull Requests **não** disparam deploy. O job roda em `ubuntu-latest` — publicar para `win-x86`/IIS não exige que o runner seja Windows.

### Migrations em produção

Diferente de projetos com banco externo (ex.: Postgres em outro provedor), o MSSQL free do MonsterASP.NET só aceita **"Local access"**: conexão de dentro do próprio datacenter deles. Um runner do GitHub Actions nunca alcançaria esse banco, então não existe etapa de `dotnet ef database update` no workflow — a própria aplicação aplica as migrations pendentes sozinha ao subir (`Database.Migrate()` logo após `builder.Build()` no `Program.cs`).

### Pré-requisitos no painel do MonsterASP.NET

1. Ativar o acesso **FTP** no painel de hospedagem (aba *Deploy* do site).
2. Ter um banco MSSQL provisionado (Databases → Create) e pegar a connection string em **Local access for websites** (é a que a aplicação, já rodando no site, consegue usar — a aba "Remote access" não serve aqui).

### Secrets necessários no repositório (Settings → Secrets and variables → Actions)

| Secret | De onde vem |
|---|---|
| `FTP_SERVER` | Painel MonsterASP.NET → site → FTP access → Hostname (ex.: `siteXXXXX.siteasp.net`) |
| `FTP_USERNAME` | Painel MonsterASP.NET → site → FTP access → Login |
| `FTP_PASSWORD` | Painel MonsterASP.NET → site → FTP access → Password |
| `DB_CONNECTION_STRING` | Painel MonsterASP.NET → banco → Local access → connection string |
| `JWT_KEY` | Uma chave longa e aleatória, só para produção (diferente da usada em dev) |
| `WEB_ORIGIN` | URL de produção do frontend no Vercel (ex.: `https://usefiscalhub.vercel.app`), usada para liberar o CORS |
| `SCALAR_USERNAME` | Usuário para acessar a documentação (`/scalar`) em produção |
| `SCALAR_PASSWORD` | Senha para acessar a documentação (`/scalar`) em produção |
| `SMTP_HOST` | *(opcional)* Host do servidor SMTP — sem isso, convite por e-mail fica desativado (o código de convite continua funcionando normalmente) |
| `SMTP_PORT` | *(opcional)* Porta do SMTP (padrão 587 se vazio) |
| `SMTP_USERNAME` | *(opcional)* Usuário/login do SMTP |
| `SMTP_PASSWORD` | *(opcional)* Senha do SMTP |
| `SMTP_FROM` | *(opcional)* E-mail remetente dos convites |

### Como a configuração chega no servidor

O MonsterASP.NET é hospedagem compartilhada: o painel não dá acesso a variáveis de ambiente do servidor/IIS diretamente. Sem `ASPNETCORE_ENVIRONMENT` definido, o ASP.NET Core assume `Production` por padrão e carrega `appsettings.Production.json` por cima do `appsettings.json`. O `appsettings.Production.json` versionado no repositório só tem placeholders vazios; a cada deploy o workflow reescreve esse arquivo já publicado (via `jq`) com os valores reais (`ConnectionStrings:DefaultConnection`, `Jwt:Key`, `Cors:AllowedOrigins`, `Scalar:Username`, `Scalar:Password`) vindos dos GitHub Secrets — os valores nunca ficam no código-fonte.

Antes de subir os arquivos por FTP, o workflow publica um `app_offline.htm` (libera os locks de arquivo do IIS) e o remove no final, colocando a aplicação de volta no ar.

### Documentação (Scalar) em produção

Em desenvolvimento, `/scalar` e `/openapi` continuam livres. Em qualquer outro ambiente, essas rotas exigem HTTP Basic Auth (usuário/senha configurados por `Scalar:Username`/`Scalar:Password`) — sem isso, respondem 401 com o desafio de Basic Auth, nunca expondo a documentação sem login. O restante da API (`/api/*`) não é afetado por essa checagem.

---

# ⭐ Diferenciais do projeto

* Estrutura em camadas (Controller + Service)
* Regras de negócio bem definidas
* Autenticação multiusuário com isolamento por família
* Segredos fora do código-fonte (User Secrets em dev, GitHub Secrets + appsettings.Production.json em produção)
* Uso de DTOs
* Documentação OpenAPI completa
* CI/CD pronto para deploy no MonsterASP.NET
* Código limpo e organizado

---

# 👨‍💻 Autor

Desenvolvido por **Pedro Gentil**

---
