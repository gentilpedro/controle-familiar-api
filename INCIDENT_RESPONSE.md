# Plano de Resposta a Incidentes de Segurança

Este documento descreve o que fazer quando um incidente de segurança envolvendo dados pessoais é identificado no Controle Familiar (API e/ou frontend). Não é um documento genérico — foi escrito depois de uma auditoria técnica de LGPD que encontrou um caso real (ver seção "Histórico" no final), e reflete o porte real do projeto: hoje mantido por uma única pessoa, sem equipe de segurança dedicada.

---

## 1. O que conta como incidente

Qualquer evento que exponha, altere ou destrua indevidamente dados pessoais tratados pela aplicação — nome, e-mail, dados de pessoas cadastradas (incluindo possíveis menores de idade) e transações financeiras. Exemplos concretos, já pensando neste projeto:

- Credencial de banco de dados, chave JWT, senha de SMTP ou qualquer outro segredo exposto (código-fonte, histórico do git, log, mensagem de erro).
- Acesso não autorizado ao banco de dados de produção.
- Falha de autorização que permita um usuário ver/alterar dados de outra família (quebra do isolamento por `FamiliaId`).
- Vulnerabilidade explorada em produção (ex.: SQL injection, bypass de autenticação).
- Perda de disponibilidade prolongada que impeça o exercício de direitos do titular (ex.: exclusão de conta fora do ar por dias).

## 2. Severidade

| Nível | Critério | Exemplo |
|---|---|---|
| **Crítico** | Dado pessoal real de terceiros exposto ou acessível publicamente; credencial de produção comprometida | Connection string de banco de produção no histórico de um repositório público |
| **Alto** | Falha que permite acesso não autorizado a dados de outra família, mesmo sem confirmação de exploração | Bug de autorização entre famílias |
| **Médio** | Exposição limitada a metadados técnicos, sem dado pessoal direto | Stack trace vazando em log |
| **Baixo** | Vulnerabilidade identificada mas sem exposição de dado até o momento da correção | Dependência desatualizada com CVE conhecido, ainda não explorado |

## 3. Passos imediatos, por tipo

### Credencial vazada (o cenário mais provável neste projeto)

1. **Rotacionar a credencial imediatamente** — senha do banco, `Jwt:Key`, senha de SMTP, o que for. Não esperar confirmar se foi explorada: trate como comprometida assim que for encontrada.
2. Se a `Jwt:Key` foi comprometida, todos os tokens já emitidos com ela viram forjáveis — considere isso equivalente a um incidente de acesso não autorizado (seção seguinte), não só troca de segredo.
3. Reescrever o histórico do git (`git filter-repo` ou BFG) **não é suficiente sozinho** se o repositório já é ou foi público — a credencial deve ser tratada como permanentemente vazada. A única correção real é a rotação.
4. Atualizar o segredo nos GitHub Secrets / User Secrets conforme o ambiente.

### Acesso não autorizado a dados (banco, API, quebra de isolamento entre famílias)

1. Identificar o vetor: como o acesso aconteceu, desde quando está aberto.
2. Corrigir a causa raiz (patch de código, revogação de credencial, etc.) antes de qualquer comunicação — não faz sentido notificar um incidente que ainda está ativo.
3. Levantar o escopo: quais famílias/usuários podem ter sido afetados, que dados especificamente.
4. Revogar sessões ativas se houver suspeita de tokens comprometidos (a tabela `TokensRevogados` já existe para isso — pode ser usada para revogação em massa se necessário).

### Vulnerabilidade descoberta sem evidência de exploração

1. Corrigir com prioridade, mas sem o mesmo senso de urgência de "já vazou".
2. Registrar como lição aprendida (seção 5) mesmo sem incidente confirmado.

## 4. Quando comunicar a ANPD e aos titulares

A LGPD (art. 48) exige comunicar a Autoridade Nacional de Proteção de Dados (ANPD) e os titulares afetados quando o incidente **possa acarretar risco ou dano relevante**. A ANPD publicou prazo específico para a comunicação preliminar — hoje, 3 (três) dias úteis contados da ciência do incidente pelo controlador (Resolução CD/ANPD nº 15/2024). Isso não é um conselho jurídico definitivo: **em caso de incidente real com indício de dado pessoal exposto, considere consultar um advogado especializado em proteção de dados antes de decidir não comunicar** — o custo de notificar sem necessidade é bem menor que o de deixar de notificar quando era obrigatório.

Na dúvida entre severidade Alto e Crítico, trate como Crítico e comece a preparar a comunicação — é mais barato interromper uma comunicação em preparo do que atrasar uma que era obrigatória.

## 5. Depois do incidente

Para qualquer incidente Médio ou acima:

1. Documentar o que aconteceu, quando foi descoberto, quando foi corrigido e o que foi feito — um parágrafo já basta, o importante é existir.
2. Adicionar um teste de regressão quando a causa for um bug de código (autorização, validação, etc.).
3. Revisitar se o mesmo padrão existe em outro lugar do código (um bug de isolamento por família em um Service provavelmente vale checar nos outros Services).

## 6. Quem aciona / é acionado

Hoje o projeto é mantido por uma única pessoa (Pedro Gentil), então "acionar a equipe" é, na prática, parar o que estiver fazendo e tratar o incidente com a prioridade da tabela da seção 2. Se o projeto ganhar mais mantenedores, esta seção deve ser atualizada com um canal de contato real (hoje não existe um e-mail de segurança dedicado — usar o mesmo contato informado na Política de Privacidade do frontend).

---

## Histórico

- **2026-07**: uma auditoria técnica de LGPD encontrou uma connection string real de banco de dados (Neon Postgres, incluindo usuário e senha) commitada no início do histórico deste repositório, que é público desde então. A credencial foi removida do arquivo em um commit posterior, mas permaneceu recuperável via `git log` por não ter sido rotacionada até a auditoria. Esta é a motivação direta para este documento existir — trate como o caso de referência da seção 3 ("Credencial vazada").
