# Roteiro da apresentação — banca de 04/12/2026

Estado: **proposta para o ensaio de 30/11/2026**. Complementa `docs/APRESENTACAO.md` (rascunho do tech lead, B10, issue #10) e `docs/COMO-MOSTRAR.md`, sem substituí-los. Mantém os mesmos princípios: demo **sem internet**, adaptadores **simulados** identificados na tela, cada integrante explica **seu código e um teste**, limitações declaradas. A diferença: aqui a demonstração tem 10 minutos e o tempo é dividido em partes iguais.

Tudo o que está abaixo foi conferido no código da `main` em 27/11/2026 (commit `3ab271d`, merge da #54). Onde algo depende de PR ainda aberta, está marcado.

---

## 0. Antes de começar (checklist do dia)

- [ ] Banco local **novo**, com nome `coreletter_demo*`, migrações aplicadas e `scripts/Seed-Demo.sql` carregado (o script recusa outro nome de banco). Passo a passo: `docs/VALIDACAO-LOCAL-2026-11-23.md`, seção "Reproduzir com banco descartável".
- [ ] API + Angular na mesma origem: `docker compose up --build` → http://localhost:8080 (README, "Demonstração local com um comando"), **ou** a publicação isolada da validação de 23/11.
- [ ] **Não** rodar o coletor nem a etapa de auditoria nesse banco: o seed é fictício e a comparação deve ser pedida ao vivo.
- [ ] Duas contas fictícias já cadastradas: `leitor@example.invalid` (plano Grátis, 0 de 3) e `admin@example.invalid` promovida com `BootstrapAdminEmail` (ganha plano Plus). Depois da promoção, **sair e entrar de novo** com o admin: o papel `admin` vai no cookie no momento do login (`SignIn` em `backend/RadarPlural.Api/Program.cs`).
- [ ] Uma publicação no feed com uma denúncia feita pelo leitor (para a tela de Moderação não aparecer vazia).
- [ ] Abas abertas no navegador: app (`/`), `/api/plans`, `/docs` (Scalar), o repositório no VS Code com os arquivos citados abaixo.
- [ ] Vídeo de abertura renderizado (`video/roteiros/proposta.json`, ver `docs/COMO-MOSTRAR.md`) e vídeo de reserva gravado (seção 4).
- [ ] Confirmar se a PR #53 (select do filtro de tema) e a correção do painel de comparação (issue #48) entraram na `main`. **Se não entraram, não mostrar o filtro de tema** (ver "Riscos" na seção 4).

---

## 1. Roteiro da demonstração (10 min)

> **Divisão sugerida** (2 min 30 s por pessoa). Os temas de cada um seguem `docs/APRESENTACAO.md` e `docs/EQUIPE.md`; a equipe pode trocar. Cada pessoa fecha seu bloco mostrando **um teste** do próprio trecho.

| Tempo | Quem (sugestão) | O que mostra | Teste que explica |
|---|---|---|---|
| 0:00–2:30 | Hugo | Problema, proposta, coleta e agrupamento | `IngestionTests` ou `ArchitectureTests` |
| 2:30–5:00 | Lindomar | Interface: histórias, feed, estados, acessibilidade | (ver nota sobre testes de front) |
| 5:00–7:30 | Davidy | Comparação + **regra de rentabilidade** ao vivo | `ProfitabilityTests.PaidPlanMustKeepMinimumMargin` |
| 7:30–10:00 | Henrique | Validação, permissões e moderação; fechamento com limitações | `DomainTests.SharedLinksOnlyAllowAbsoluteHttpWithoutCredentials` ou `scripts/Test-Api.ps1` |

### Bloco 1 — Hugo (0:00–2:30): problema e coleta

1. **(0:00–0:40)** Vídeo de abertura "De pessoas para pessoas, sem algoritmo" (1 min; se o tempo apertar, cortar em 40 s). Frase-guia: *"A mesma notícia é contada de jeitos diferentes; o Coreletter junta os veículos e mostra onde concordam e onde divergem."*
2. **(0:40–1:40)** Terminal: `dotnet run --project backend/RadarPlural.Worker -- --dry-run`.
   - Só funciona com internet (lê os feeds reais de `backend/RadarPlural.Worker/fontes.json`). **Sem rede, pular para o print** do dry-run (seção 4).
   - Apontar: descartes com motivo (oferta/afiliado, resumo do dia, mais de 7 dias — `backend/RadarPlural.Ingestion/Triage.cs`) e histórias com 2+ fontes (`StoryClustering.cs`: 3+ palavras relevantes em comum, ao menos 1 nome próprio, até 48 h).
3. **(1:40–2:10)** Segurança da coleta em uma frase: só hosts da lista aprovada, sem credenciais na URL, porta padrão, e checagem do IP **depois** da resolução DNS contra rede interna (`backend/RadarPlural.Ingestion/UrlGuard.cs`, `SafeFeedFetcher.cs`). Política: `docs/decisoes/0002-ingestao.md`.
4. **(2:10–2:30)** Teste: abrir `backend/RadarPlural.Tests/ArchitectureTests.cs` — "se alguém fizer o domínio depender do banco, este teste quebra". Passa a palavra para Lindomar.

### Bloco 2 — Lindomar (2:30–5:00): interface

1. **(2:30–3:10)** Abrir http://localhost:8080. A tela inicial é o **Feed da comunidade** (`view = "feed"` em `frontend/src/main.ts`). Mostrar o filtro por tema/formato (Notícias, Podcasts, Músicas) e clicar em **Filtrar**.
2. **(3:10–3:50)** Clicar em **Entrar** → login com `leitor@example.invalid`. Clicar em **＋ Compartilhar descoberta** → preencher link `https://exemplo.example.invalid/artigo`, título, tema, formato → publicar. Aparece o aviso "Sua descoberta foi publicada na comunidade." Clicar em **Salvar** no card e abrir **Itens salvos** na barra lateral.
3. **(3:50–4:30)** Barra lateral → **Histórias e fontes**. Mostrar o selo "Plano Grátis · 0 de 3 comparações no mês", os cards "2 fontes" e os links de cada veículo (abrem em nova aba com `rel="noopener noreferrer"`).
   - Filtro de tema: **só se a PR #53 estiver na `main`**. Com o seed atual, "Ciência" mostra as 8 histórias e qualquer outro tema mostra a mensagem de vazio (isso também demonstra o estado vazio).
4. **(4:30–5:00)** Acessibilidade e estados: `aria-current` na barra lateral, `role="status"` no "Carregando descobertas…" e no selo de quota, diálogos com `aria-labelledby` (`frontend/src/app.html`). Layout conferido em 1280×900 e 390×844 (`docs/VALIDACAO-LOCAL-2026-11-23.md`).
   - **Teste:** o front **não tem testes automatizados de comportamento** (`npm test` só compila — dito na própria validação de 23/11). Lindomar explica o componente `frontend/src/comparison-panel.ts` (extraído na PR #43) e a correção da PR #53, e diz com honestidade que o teste de interface foi manual (prints no grupo/PR).

### Bloco 3 — Davidy (5:00–7:30): comparação e regra de rentabilidade

1. **(5:00–5:50)** Em **Histórias e fontes**, clicar em **Comparar fontes** na história "DEMO FICTÍCIA 5".
   - O front primeiro tenta `GET /api/stories/{id}/comparison` (pronta, pública, não gasta quota); como não existe, chama `POST /api/comparisons` com um `Idempotency-Key` novo (`compare()` em `frontend/src/main.ts`).
   - Aparece "Comparação feita: 1 uso do seu plano neste mês." e o selo vai para **1 de 3**.
   - No painel: aviso "Comparação simulada", contagem concordam/divergem/só uma fonte, evidências com o nome da fonte e a lista de limitações. As fontes fictícias dizem "100 participantes" × "200 participantes": é o caso de **divergência numérica** do `SimulatedAuditor` (`backend/RadarPlural.Infrastructure/SimulatedAuditor.cs`).
2. **(5:50–6:20)** Comparar "DEMO FICTÍCIA 6" e "7" → **2 de 3**, **3 de 3**. Clicar em "Fechar comparação" e abrir de novo a 5: **não desconta** (cache de 24 h, `CompareStory.CacheFor`).
3. **(6:20–6:40)** Comparar "DEMO FICTÍCIA 8" → erro "Você usou todas as comparações do seu plano neste mês." (HTTP 403 `quota_esgotada`, RN01).
4. **(6:40–7:10)** Aba `/api/plans`: ler os números ao vivo (RN02 e RN04, `docs/RENTABILIDADE.md`):
   - Plus: custo máximo = 60 × R$ 0,08 + R$ 0,50 = **R$ 5,30**; margem = (19,90 − 5,30) ÷ 19,90 = **73,4%**; preço mínimo = 5,30 ÷ 0,75 = **R$ 7,07**; equilíbrio = 800 ÷ 14,60 = **55 assinantes**.
   - Grátis: 3 × 0,08 = R$ 0,24 ≤ R$ 0,30 de custo de aquisição aceito.
   - "Se alguém baixar o Plus para R$ 7,06, **a API não sobe**": laço `Profitability.Check` no início de `backend/RadarPlural.Api/Program.cs`.
5. **(7:10–7:30)** Teste: `backend/RadarPlural.Tests/ProfitabilityTests.cs`, `PaidPlanMustKeepMinimumMargin` (19,90 ok; 7,07 ok; 7,06 falha; 5,30 falha). Se sobrar fôlego: `PgUsageLedgerTests.ConcurrentRequestsNeverExceedQuota` (`PgStoryQueueTests.cs`), reserva atômica no PostgreSQL (`PgUsageLedger.cs`).

### Bloco 4 — Henrique (7:30–10:00): validação, permissões, moderação e fechamento

1. **(7:30–8:10)** Validação: em **＋ Compartilhar descoberta**, tentar um link `ftp://...` → o front recusa ("Informe um link http ou https."). Na aba `/docs` (Scalar), enviar `POST /api/posts` com título vazio → **400** com a mensagem de `Program.cs` (link http(s), título até 180, tema até 60, tipo válido, comentário até 2000). Regra de URL: `Rules.ValidUrl` em `backend/RadarPlural.Api/Domain.cs`.
2. **(8:10–8:40)** Permissões: com o leitor, clicar em **Denunciar** num post → digitar o motivo (mínimo 5 caracteres). Mostrar que o leitor **não vê** a aba Moderação (só aparece se `isAdmin`).
3. **(8:40–9:10)** **Sair** → entrar com `admin@example.invalid` → **Moderação** → **Ocultar** → o post some do feed e a denúncia sai da fila (`GET /api/moderation/reports` só lista posts não ocultos; teste da PR #41).
4. **(9:10–9:30)** Teste: `DomainTests.SharedLinksOnlyAllowAbsoluteHttpWithoutCredentials` (`backend/RadarPlural.Tests/UnitTest1.cs`) **ou** um trecho de `scripts/Test-Api.ps1` (isolamento entre contas, Origin, admin), que a CI roda contra PostgreSQL.
5. **(9:30–10:00)** Fechamento (Henrique lê, todos de pé): as **limitações** de `docs/APRESENTACAO.md` — comparação só sobre manchete e resumo; agrupamento por palavras; concordância não prova verdade; IA e pagamento simulados, custos são hipóteses. Próximo passo: adaptador Gemini (B09) registrando outro `IAuditor` em `Program.cs`, sem mudar o caso de uso.

**Folga:** não há. Se um bloco estourar 20 s, o próximo corta o item "se sobrar fôlego". O slide de arquitetura (seção 2) entra **depois** da demo, nas perguntas, ou num bloco extra se a banca permitir mais de 10 min.

---

## 2. Slide de arquitetura (roteiro)

Fonte: código da `main`. Resumo da regra de dependência em `docs/README.md` e `docs/decisoes/0006-monolito-modular.md`.

### Slide A — Camadas (monólito modular, arquitetura limpa)

```
 Angular (frontend/src)  ──HTTP mesma origem──►  RadarPlural.Api (Program.cs)      RadarPlural.Worker (Program.cs)
                                                     │  compõe adaptadores               │  coleta / --etapa auditoria
                                                     ▼                                   ▼
                                   RadarPlural.Pipeline (casos de uso + portas: CompareStory, IAuditor, IStoryQueue)
                                                     │
                     RadarPlural.Domain (Billing: PlanCatalog, Profitability, UsageRules, IUsageLedger)
                     RadarPlural.Ingestion (FeedParser, Triage, StoryClustering, UrlGuard)
                                                     ▲
                     RadarPlural.Infrastructure (PgUsageLedger, PgStoryQueue, PgAuditStore, SimulatedAuditor)
                                                     │
                                                PostgreSQL
```

- **Domínio** sem dependências: `backend/RadarPlural.Domain/RadarPlural.Domain.csproj` não referencia nenhum projeto nem pacote; `RadarPlural.Ingestion.csproj` idem.
- **Aplicação**: `backend/RadarPlural.Pipeline` só referencia o Domain. Portas em `Ports.cs` (`IStoryReader`, `IAuditor`, `IAuditStore`) e `Stages.cs` (`IStoryQueue`).
- **Infraestrutura**: `backend/RadarPlural.Infrastructure` implementa as portas com Npgsql.
- **Entradas**: `RadarPlural.Api` (web) e `RadarPlural.Worker` (linha de comando/CronJob).
- **Garantia automática**: `backend/RadarPlural.Tests/ArchitectureTests.cs` (4 testes de dependência).

### Slide B — Entidades principais (`backend/RadarPlural.Api/Domain.cs`, `AppDb`)

| Grupo | Entidades | Para quê |
|---|---|---|
| Comunidade | `Member`, `Post`, `SavedPost`, `Report` | Contas (com `IsAdmin`, `PlanId`), publicações, salvos privados, denúncias (`Post.Hidden` para moderação) |
| Notícias | `FeedItem`, `Story` | Item coletado (status de triagem, motivo de descarte) e história agrupada (status, `Version`, lease do pipeline) |
| Comparação | `StoryAudit` | Resultado do contrato v1 por versão da história (`docs/contratos/comparacao-v1.md`) |
| Cobrança simulada | `UsageMonth`, `UsageReservation` | Uso mensal por membro e reserva idempotente (RN01/RN03) |
| Infra | `DataProtectionKey` | Chaves do cookie no banco: reinício não derruba sessões |

Esquema versionado: 6 migrações em `backend/RadarPlural.Api/Migrations` (Inicial → ItensFeed → TriagemHistorias → EtapasPipeline → Rentabilidade → Comparacoes).

### Slide C — Integrações e implantação

| Integração | Estado real no código |
|---|---|
| Feeds RSS/Atom de fontes aprovadas | **Real**: `backend/RadarPlural.Worker/fontes.json` + `SafeFeedFetcher.cs`; GitHub Actions a cada 3 h (`.github/workflows/ingestao.yml`) |
| IA (comparação) | **Simulada**: `SimulatedAuditor` registrado em `Program.cs`. Gemini (B09) não existe no código |
| Pagamento | **Não existe**: troca de plano só por admin (`PUT /api/admin/members/{id}/plan`) |
| Documentação da API | `/docs` (Scalar) e `/openapi/v1.json` |
| Vídeo | HyperFrames fora do produto (`video/`), renderizado à parte |
| Deploy | `Dockerfile` (API + Angular numa imagem), `render.yaml`, `compose.yaml`; Kubernetes local em `deploy/k8s` (API 2 réplicas, Job de migração, CronJob do coletor `17 */3 * * *`, auditoria com 0 réplicas) |
| CI | `.github/workflows/ci.yml`: testes .NET com PostgreSQL, `Test-Api.ps1`, build Angular, testes do gerador de vídeo, validação dos manifests k8s, build das imagens |

---

## 3. Perguntas prováveis da banca (respostas curtas)

1. **Como vocês testaram?** — xUnit em `backend/RadarPlural.Tests`: regras puras (rentabilidade, triagem, agrupamento, contrato), integração com PostgreSQL real (`[IntegrationFact]`, roda com `INTEGRATION_DATABASE_URL`) e cenários HTTP (`scripts/Test-Api.ps1`, `scripts/Test-Comparisons.ps1`). Em 23/11: **258 testes aprovados, 0 falhas, 0 pulados** com PostgreSQL (`docs/VALIDACAO-LOCAL-2026-11-23.md`). A CI roda tudo a cada PR.
2. **E o frontend tem testes?** — Não há testes automatizados de comportamento; `npm test` só compila. A validação da interface foi manual (navegador, dois tamanhos de tela). É uma limitação declarada.
3. **Onde está a regra de rentabilidade e como provam que funciona?** — `backend/RadarPlural.Domain/Billing/Plans.cs` e `Catalog.cs`. Casos limite em `ProfitabilityTests.cs` (7,07 passa, 7,06 falha; 59 permite, 60 bloqueia; mês em UTC). A API se recusa a subir se um plano violar a margem.
4. **Dois pedidos ao mesmo tempo podem passar da quota?** — Não. `PgUsageLedger.ReserveAsync` incrementa com `WHERE "Used" < quota` no mesmo comando SQL, dentro de transação. Teste: `ConcurrentRequestsNeverExceedQuota` (50 pedidos, quota 10 → exatamente 10).
5. **E se o usuário clicar duas vezes?** — Cada pedido leva `Idempotency-Key`; a mesma chave não reserva duas vezes. Chave reaproveitada sem resultado em cache → **409 `chave_reutilizada`** (PR #52, teste `ReusedIdempotencyKeyAcrossDifferentStoriesReturns409ProblemDetails`). Se o provedor falha, a unidade é devolvida (`ReleaseAsync`).
6. **Por que monólito e não microsserviços?** — Decisão 0006: transação atômica na quota com banco único, equipe de 4, prazo curto. A escala vem de réplicas por processo (API sem estado, etapas com `FOR UPDATE SKIP LOCKED` em `PgStoryQueue.cs`) — decisão 0004.
7. **Por que portas e adaptadores?** — Para trocar o auditor simulado pelo Gemini registrando outro `IAuditor` em `Program.cs`, sem mexer em `CompareStory`. `ArchitectureTests` impede o domínio de depender de banco ou web.
8. **Como a autenticação é protegida?** — Cookie `HttpOnly`, `SameSite=Strict`, `Secure` fora de desenvolvimento, expira em 8 h; senha com `PasswordHasher` (hash com sal, teste `PasswordHashesAreSaltedAndRejectWrongPassword`); middleware recusa mutação com `Origin` de outro host (CSRF); limite de requisições por minuto (padrão 60) e corpo máximo de 32 KB (`Program.cs`).
9. **Como impedem um usuário de agir como outro?** — A API usa sempre o id do cookie (`Id(ctx)`), nunca o enviado no corpo; excluir post só o autor; moderação e troca de plano exigem papel `admin`. `Test-Api.ps1` testa isolamento de salvos entre contas.
10. **Coletar notícias não é risco de SSRF ou de direito autoral?** — Só RSS/Atom de lista aprovada, guarda apenas título, link, data e resumo de até 300 caracteres; `UrlGuard` barra host fora da lista, credenciais na URL, porta não padrão e IP interno depois do DNS. Links de usuários **não são baixados** (`backend/API.md`). Decisão 0002.
11. **A IA pode inventar citações?** — Toda saída de qualquer auditor passa por `ContractValidator.Sanitize` (`backend/RadarPlural.Pipeline/Comparison/Contract.cs`) antes de ser gravada. O simulado só cita trechos literais do título/resumo.
12. **Quais as limitações?** — Comparação sobre manchete e resumo, não texto completo; agrupamento por palavras (sem sinônimos); concordância não é verdade; IA e pagamento simulados; números de custo são hipóteses; sem testes de front; o filtro de tema e o painel tiveram regressões recentes (issue #48).
13. **Isso escala?** — Preparado: API sem estado (2 réplicas em `deploy/k8s/10-api.yaml`), migração em Job único (`02-migrate-job.yaml`), fila no banco, teto de custo testado (`backend/RadarPlural.Tests/Infra/CostCeilingTests.cs`, `deploy/k8s/custo.json`), script de carga em `scripts/carga/`. Não foi medido em produção.
14. **Por que existe `UsageBudget` e `FinancialRules` em `Domain.cs` da API se a regra está em `RadarPlural.Domain`?** — São da base inicial (regra em memória, com testes em `UnitTest1.cs`). A regra usada pelos endpoints é a de `RadarPlural.Domain/Billing` + `PgUsageLedger`. Remover o código antigo é dívida técnica conhecida.
15. **O que cada um fez?** — (preencher pela equipe, com links de PR/commit reais; não atribuir código gerado por IA sem revisão própria — `docs/EQUIPE.md`)
    - Hugo: `[PREENCHER: PRs, arquivos, teste que explica]`
    - Davidy: `[PREENCHER]`
    - Lindomar: `[PREENCHER]`
    - Henrique: `[PREENCHER]`

---

## 4. Plano B — se a demo ao vivo falhar

`docs/APRESENTACAO.md` e `docs/COMO-MOSTRAR.md` já preveem: gravação de tela e o vídeo "Por dentro do Coreletter". Este é o checklist para deixar isso pronto até 30/11.

### 4.1 Gravação de tela (até domingo 29/11)
- [ ] Um vídeo único de ~4 min seguindo **exatamente** os blocos 2, 3 e 4 acima, com o banco seed novo, em 1280×720 ou maior, sem notificações na tela.
- [ ] Salvo em 2 lugares: pendrive **e** notebook de quem apresenta (não depender de nuvem). MP4 H.264.
- [ ] Vídeos HyperFrames renderizados: `proposta`, `tour-do-codigo`, `como-funciona` (comandos em `docs/COMO-MOSTRAR.md`).

### 4.2 Prints (um por passo, numerados, numa pasta `plano-b/`)
- [ ] Saída do `--dry-run` com descartes e histórias (gravar com internet antes; na banca não haverá).
- [ ] Feed com publicação; diálogo de compartilhar; Itens salvos.
- [ ] Histórias com selo "0 de 3"; painel de comparação aberto com divergência; selo "3 de 3"; erro de quota na 4ª.
- [ ] `/api/plans` com os números; `ProfitabilityTests` passando no terminal.
- [ ] Erro 400 no `/docs`; Moderação com denúncia; post ocultado.
- [ ] Saída de `dotnet test` (258 aprovados) e da CI verde.

### 4.3 Dados offline
- [ ] Banco 100% local (Docker ou PostgreSQL instalado), sem Render: o PostgreSQL gratuito do Render expira ~26/11/2026 (`docs/COMO-MOSTRAR.md`), então **não contar com https://coreletter.onrender.com**.
- [ ] `scripts/Seed-Demo.sql` aplicado num banco **recém-criado**: comparações ficam em cache público, então uma segunda rodada no mesmo banco não mostra o consumo 1→2→3. Para ensaiar de novo, criar outro banco `coreletter_demo_*`.
- [ ] Imagens Docker (`postgres:17-alpine` e a do app) **já construídas** antes de sair de casa: `docker compose up --build` sem internet falha se precisar baixar camadas ou pacotes NuGet/npm.
- [ ] Um segundo notebook com o mesmo ambiente pronto.

### 4.4 Riscos conhecidos no código (conferir no ensaio)
- **Filtro de tema das histórias quebrado na `main`**: `frontend/src/app.html` (bloco `@if(view==='stories')`) mostra o texto solto "Tema Todos os temas…" sem `<select>`. Correção na PR #53 (aberta). Até entrar, não mostrar.
- **Painel de comparação sem estilo e sem `aria-live`**: `frontend/src/comparison-panel.html` são só `<p>` sem classes (regressão da #43, issue #48). Funciona, mas fica feio na projeção.
- **Seed tem um tema só** (`ciencia`) e só 2 fontes por história (issue #50). Qualquer filtro além de Ciência mostra vazio.
- **Admin precisa relogar** depois da promoção por `BootstrapAdminEmail`, senão a aba Moderação não aparece.
- **Coleta ao vivo depende de internet** e de os feeds responderem; usar o print.
- Chave de idempotência de outro membro gera exceção não tratada (500) em `PgUsageLedger.ReserveAsync` — não acontece no fluxo normal (o front gera UUID novo), só se alguém forçar pelo `/docs`.
