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
cd ControleFamiliarAPI/ControleFamiliarAPI

dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=localhost,14330;Database=ControleFinanceiro;User Id=sa;Password=<sua-senha-do-.env>;TrustServerCertificate=True;"

dotnet user-secrets set "Jwt:Key" "<uma-chave-longa-e-aleatoria>"
```

Se `ConnectionStrings:DefaultConnection` ou `Jwt:Key` não estiverem configurados, a aplicação falha no startup com uma mensagem explicando o que fazer — isso é intencional, para nunca subir silenciosamente sem segredo configurado.

## 3️⃣ Restaurar dependências

```bash
dotnet restore
```

## 4️⃣ Rodar as migrations

```bash
dotnet ef database update
```

## 5️⃣ Executar a aplicação

```bash
dotnet run
```

## 6️⃣ Acessar a documentação

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

O workflow `.github/workflows/deploy-monsterasp.yml` builda, aplica as migrations pendentes no banco de produção, publica e faz deploy da API para o [MonsterASP.NET](https://monsterasp.net/) via FTP.

### Quando roda

* Automaticamente em todo push na branch `main`.
* Manualmente, a qualquer momento, pelo botão **Run workflow** na aba *Actions* do GitHub.

Branches de feature e Pull Requests **não** disparam deploy.

### Pré-requisitos no painel do MonsterASP.NET

1. Ativar o acesso **FTP** no painel de hospedagem.
2. Ter um banco SQL Server de produção provisionado (no próprio MonsterASP.NET ou outro provedor) e a connection string em mãos.

### Secrets necessários no repositório (Settings → Secrets and variables → Actions)

| Secret | De onde vem |
|---|---|
| `FTP_SERVER` | Painel MonsterASP.NET (host do FTP) |
| `FTP_USERNAME` | Painel MonsterASP.NET (usuário do FTP) |
| `FTP_PASSWORD` | Painel MonsterASP.NET (senha do FTP) |
| `DB_CONNECTION_STRING` | Connection string do SQL Server de produção |
| `JWT_KEY` | Uma chave longa e aleatória, só para produção (diferente da usada em dev) |
| `WEB_ORIGIN` | URL de produção do frontend no Vercel (ex.: `https://controle-familiar-web.vercel.app`), usada para liberar o CORS |

### Como a configuração chega no servidor

O MonsterASP.NET é hospedagem compartilhada: o painel não dá acesso a variáveis de ambiente do servidor/IIS diretamente. Sem `ASPNETCORE_ENVIRONMENT` definido, o ASP.NET Core assume `Production` por padrão e carrega `appsettings.Production.json` por cima do `appsettings.json`. O `appsettings.Production.json` versionado no repositório só tem placeholders vazios; a cada deploy o workflow reescreve esse arquivo já publicado com os valores reais (`ConnectionStrings:DefaultConnection`, `Jwt:Key`, `Cors:AllowedOrigins`) vindos dos GitHub Secrets — os valores nunca ficam no código-fonte.

Antes de subir os arquivos por FTP, o workflow publica um `app_offline.htm` (libera os locks de arquivo do IIS) e o remove no final, colocando a aplicação de volta no ar.

### Runtime

O publish usa `--runtime win-x86` (padrão recomendado pelo MonsterASP.NET, a maioria dos planos usa app pool de 32 bits). Se o seu plano usar app pool de 64 bits, troque para `win-x64` no workflow.

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
