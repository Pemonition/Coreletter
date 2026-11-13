# Estado da base automatizada

Data: 26/10/2026. Produto Coreletter; namespaces .NET e nome técnico npm ainda RadarPlural para preservar os contratos iniciais. Isso não representa uma segunda aplicação.

## Implementado

- API .NET 10 com PostgreSQL, autenticação cookie, publicação de links, feed paginado com filtros, exclusão pelo autor, salvamento privado, denúncia e ocultação administrativa.
- Angular integrado à API por proxy local, com estados de carregamento/erro/vazio e sem mocks silenciosos.
- Cálculos RN01/RN03 e reserva de quota em memória com testes. A regra financeira ainda não está conectada ao fluxo da aplicação.
- Scripts PowerShell, CI, acordo de equipe, backlog e instruções compartilhadas entre assistentes.

## Validação executada localmente

- Back-end: 21 testes aprovados em Release; zero avisos e erros de compilação.
- Front-end: build de produção aprovado; auditoria de dependências de produção sem vulnerabilidades reportadas no momento da consulta.
- Scripts PowerShell: análise sintática aprovada. Isso não comprova a execução de cada script.
- Revisão independente estática do cenário de integração e das consultas de isolamento.

## Publicação e validação remota

Repositório público: https://github.com/Pemonition/Coreletter. Base inicial: commit 86234ed. CI aprovada: https://github.com/Pemonition/Coreletter/actions/runs/40426458380. O job backend executou os 21 testes e o cenário scripts/Test-Api.ps1 com PostgreSQL real descartável: cadastro, publicação, exclusão negada a terceiro, salvamento idempotente/privado, denúncia, restrição de moderação e rejeição de entradas inválidas. O job frontend instalou pelo lockfile e compilou a produção. Isso não substitui o teste visual/manual do navegador nem verifica ocultação como administrador, que continua no roteiro B01/B08.

Convites com escrita enviados a ddanjos, LindomarSSJ e hikmiau; precisam de aceite. Dez issues abertas e atribuídas conforme docs/BACKLOG.md. E-mails pessoais da ficha não foram publicados. Git local usa endereço noreply da conta do tech lead.

## Limites

Docker Desktop não disponibilizou o engine local nas tentativas realizadas. Integração PostgreSQL foi aprovada na CI, mas uso completo no navegador e execução em outra máquina ainda precisam de validação. Não atribuir ao ambiente local a validação que ocorreu remotamente.

Não foram implementados Gemini real, scraping automático, comparação de fontes, assinatura persistente, pagamentos, transcrição ou publicação em redes. O domínio financeiro em memória não substitui uma reserva transacional no banco.

O código inicial foi produzido com assistência automatizada, não comprova contribuições individuais dos quatro alunos. Cada integrante deverá assumir suas tarefas, registrar commits próprios e explicar alterações e testes.

## Deploy de demonstração (27/10/2026, branch feat/deploy-render)

Preparação para Render gratuito descrita em `docs/decisoes/0001-deploy-render.md`: Dockerfile único (Angular servido pela API), `render.yaml` com PostgreSQL gratuito, migração `Inicial` no lugar de `EnsureCreated`, chaves de sessão no banco, porta via `PORT`, conversão de URL `postgresql://` com testes (RED com 2 falhas esperadas, depois GREEN). Localmente: 24 testes aprovados em Release com zero avisos e build Angular de produção aprovado. Docker não estava disponível localmente: imagem, migração em PostgreSQL vazio e `Test-Api.ps1` contra o container ficam com o job `container` da CI. A URL pública e o banco expiram conforme os limites do plano gratuito.

## Coletor de notícias (12/11/2026, branch feat/ingestao-rss, sobre feat/deploy-render)

Política em `docs/decisoes/0002-ingestao.md`. Novos projetos: `RadarPlural.Ingestion` (download seguro, parser RSS/Atom, normalização de URL, catálogo de fontes) e `RadarPlural.Worker` (console agendado pelo GitHub Actions). Migração `ItensFeed` (tabela `FeedItems`, URL única) e `GET /api/news` na API.

TDD registrado: RED com 62 falhas esperadas (`NotImplementedException`) e 24 aprovados; GREEN com 86. Catálogo de fontes: RED 9, GREEN 95. Endurecimento (pontuação, resumo curto): RED 2, GREEN 101. Teste de loopback na conexão já passou de primeira: a defesa existia, o teste a comprova. Final: 101 testes aprovados em Release, zero avisos.

Validado localmente com PostgreSQL 16 real e rede: API aplicou as migrações; coletor leu 7 fontes, gravou 200 itens; segunda execução gravou 0 (deduplicação); `/api/news` com filtros por tema e fonte e erro 400 para página inválida; resumos até 299 caracteres. g1 e Folha retornaram 403 ao robô identificado e foram retirados da lista. Ainda não executados: o workflow `ingestao.yml` no GitHub (só dispara da `main`; testar por *Run workflow*) e a gravação no banco do Render (depende do secret).

## Triagem e agrupamento (12/11/2026, branch feat/triagem-agrupamento, sobre feat/deploy-render)

Decisão 0003 (PR de documentação separada). `Triage` e `StoryClusterer`/`StoryReconciler` em `RadarPlural.Ingestion`; `StoryStore` no Worker; migração `TriagemHistorias` (status/motivo/marcas em `FeedItems`, tabela `Stories`); `GET /api/stories`; `/api/news` só com notícias aprovadas.

TDD: RED 33 falhas esperadas → GREEN 134. Segunda rodada com dados reais revelou 3 erros de agrupamento (encadeamento via "CEO", "GTA 6 + Xbox" juntando eventos diferentes, maiúscula após "?" tratada como nome) e guias de compra/resumo do dia passando: viraram 10 testes RED → GREEN 143. Regra "2 nomes próprios" removida por causa disso.

Validado com PostgreSQL 16 e feeds reais: ~200 notícias por coleta, ~50 descartadas (42 por caminho de afiliado), ~130 histórias, 12 com 2+ fontes. Reexecução mantém ids; divisão de história errada manteve o id na parte maior; zero histórias órfãs; contagens conferidas por SQL. Limitações: regra por palavras não entende sinônimos ("renuncia" × "deixa") e depende de nome próprio; mesma história com manchetes muito diferentes pode ficar separada (ex.: "Epic Games" × "Jogos grátis na Epic Games"); manchetes TODAS EM MAIÚSCULAS contam todas as palavras como nome.

## Escala desde o MVP (12/11/2026, branch feat/escala-pipeline, sobre feat/triagem-agrupamento)

Decisão `docs/decisoes/0004-escala-desde-o-mvp.md`. Projetos novos: `RadarPlural.Pipeline` (casos de uso e portas: `StageRunner`, `RetryPolicy`, `IStoryQueue`, `IAuditor`…) e `RadarPlural.Infrastructure` (adaptadores PostgreSQL: `PgStoryQueue`, e `FeedItemStore`/`StoryStore` movidos do Worker). Worker `--etapa`; API `/api/health/ready`, `--migrate-only`, `Database__MigrateOnStartup`, `RateLimit__PermitPerMinute`; `deploy/k8s`; `Dockerfile.worker`; `scripts/carga`.

TDD: executor RED 26 → GREEN (o teste "notícia nova devolve a história ao início" passou de primeira porque o SQL foi escrito antes do teste: registrado como tal). Teto de custo RED 13 → GREEN. Testes de arquitetura escritos depois (trava). Total **186 testes**, 7 de integração com PostgreSQL (ignorados sem `INTEGRATION_DATABASE_URL`; a CI define). Mutações conferidas: sem `SKIP LOCKED` o teste de concorrência falha 3/3; `maxReplicas: 9` estoura o teto; `Pipeline` dependendo de Npgsql quebra o teste de arquitetura.

Bug encontrado e corrigido: duas instâncias da API migrando juntas → uma cai. Linha de base de carga em 1 núcleo: ~480 req/s, p95 149 ms com 50 usuários, 0 erros.

## Regra de rentabilidade, versão básica (12/11/2026, branch feat/rentabilidade, sobre feat/escala-pipeline)

Projeto novo `RadarPlural.Domain` (Billing: RN01 quota, RN02 margem mínima, RN04 ponto de equilíbrio, porta `IUsageLedger`) e `PgUsageLedger` (RN03 reserva atômica). Migração `Rentabilidade` (`Members.PlanId`, `UsageMonths`, `UsageReservations`). Endpoints `/api/plans`, `/api/me/usage`, `/api/admin/members/{id}/plan`. API não inicia se um plano violar RN02 (verificado: Plus a R$ 7,06 derruba a inicialização).

TDD: regras RED 26 → GREEN; reserva RED 4 → GREEN; total 218 testes, 3/3 execuções. Mutação: sem `WHERE used < quota` o teste de 50 pedidos simultâneos falha. Feita por Hugo como base para o Davidy (B07) melhorar; o `UsageBudget` em memória antigo continua no código até a B05 trocar pelo ledger.

## Comparador simulado, versão básica (12/11/2026, branch feat/comparador-simulado, sobre feat/rentabilidade)

`ContractValidator` (contrato v1), `SimulatedAuditor`, `AuditStageHandler` (Worker `--etapa auditoria` agora funciona), `CompareStory` (cache 24 h + RN03 + devolução), `PgStoryReader`/`PgAuditStore`, migração `Comparacoes` (`StoryAudits` jsonb). API: `GET /api/stories/{id}/comparison`, `POST /api/comparisons`.

TDD: validador/auditor/etapa RED 19 → GREEN; caso de uso RED 8 → GREEN; teste de integração ponta a ponta escrito depois dos adaptadores. Total 247. Validado com feeds reais: 10 histórias corroboradas auditadas pela fila; usuário Grátis: 3 cobradas, 4ª 403, cache e idempotência sem cobrança dupla.

## Próxima sessão

1. Aceitar convites, conferir CI e executar B01 com PostgreSQL na máquina dos integrantes.
2. Corrigir os problemas observados antes de novas funcionalidades.
3. Hugo e Davidy definem contrato de comparação B04; Lindomar e Henrique executam tarefas pequenas do backlog, com revisão.
4. Registrar RED/GREEN nas próximas regras para evidenciar TDD; não inventar evidência retroativa.
5. Coletor: revisar a política 0002 na PR; depois do merge, criar o secret INGESTAO_DATABASE_URL e rodar o workflow manualmente. Próximos passos do produto: agrupar a mesma notícia de veículos diferentes (base da comparação B04/B05) e tela do feed de notícias.

Nunca colocar chaves ou dados privados neste arquivo. Atualizar o estado quando uma limitação for resolvida.
