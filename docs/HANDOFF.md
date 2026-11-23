# Estado da base automatizada

## Retomada dos quatro ciclos — 23/11/2026

Validação final da atualização: `npm test --prefix video` **14/14**, `npm run check --prefix video` **zero erros e 12 avisos** (10 lint, 2 craft), nove amostras de execução e 472 de composição; não equivale a render MP4. Relatório local em `video/composicoes/como-funciona/.hyperframes/artifacts/checks/f35253bb-614f-48f0-b8f7-5327207f8a1b.json`. `npm audit --prefix video --json` e frontend: **zero vulnerabilidades**; `dotnet list backend/RadarPlural.slnx package --vulnerable --include-transitive --format json`: nenhum pacote vulnerável nos sete projetos. `git diff --check` aprovado. Saída automática `.hyperframes/` agora ignorada no Git. Alterações preparadas com assistência de IA; não representam revisão humana.

Entrega mantida local, sem nova PR. Após revisão: `git push -u origin docs/revisao-quatro-ciclos-20261123`; depois `gh pr create --base main --head docs/revisao-quatro-ciclos-20261123 --title 'fix(video): atualizar HyperFrames para corrigir Undici' --body 'Atualiza HyperFrames 0.9.40 para 0.9.42 e Undici 7.25.0 para 7.30.1, sem overrides. Auditorias npm/NuGet sem alertas; 14 testes de video aprovados; check com zero erros e 12 avisos. MP4 ainda precisa de ensaio. Detalhes e limites em docs/HANDOFF.md.'`. Revisão humana exigida antes do merge.

Base conferida: `origin/main` em `77e49ef`; PR #42 integrada e CI da main [45283179302](https://github.com/Pemonition/Coreletter/actions/runs/45283179302) aprovada nos quatro jobs (backend, frontend, vídeo e container). O texto abaixo sobre aguardar PR da validação anterior é histórico. Nenhum deploy foi confirmado.

Branch desta retomada: `docs/revisao-quatro-ciclos-20261123`. Detectada lacuna na auditoria de vídeo: HyperFrames 0.9.40 fixava Undici 7.25.0, com alertas altos. Atualizado somente HyperFrames para 0.9.42 (primeira versão seguinte fora do intervalo vulnerável reportado), que fixa Undici 7.30.1, mantendo GSAP e o restante do lockfile. Sem force/overrides. Alcance: ferramenta local de vídeo; não é dependência da API/frontend publicados. Avisos primários: [TLS](https://github.com/nodejs/undici/security/advisories/GHSA-2m5v-pch6-74hx) e [DoS](https://github.com/nodejs/undici/security/advisories/GHSA-xjj6-f33q-929c). A presença da dependência não demonstra exploração no produto.

Nesta retomada: `npm audit --prefix frontend --json` sem alertas; `dotnet list backend/RadarPlural.slnx package --vulnerable --include-transitive --format json` sem pacotes vulneráveis nos sete projetos; instalação do vídeo após atualização sem alertas; `npm test --prefix video`: 14 aprovados, zero pulados. Não foram repetidos build, PostgreSQL e cenários HTTP já aprovados: código da aplicação idêntico à validação anterior, logs locais conferidos (258/258, zero pulados), além da CI verde da main. Capturas desktop/mobile existentes foram inspecionadas nesta retomada; não houve nova sessão manual da aplicação. Estados de carregamento/vazio/erro e acessibilidade completos não estão comprovados pelas capturas. Evidências e roteiro reproduzível permanecem em [VALIDACAO-LOCAL-2026-11-23.md](VALIDACAO-LOCAL-2026-11-23.md).

Docker local continua indisponível (pipe do engine ausente). Não foram executados feeds reais, IA paga, cobrança, deploy ou render MP4. Alterações preexistentes em `deploy/k8s/README.md`, `docs/AULAS.md` e `docs/EVIDENCIAS-AULAS.md` foram preservadas e ficam fora desta entrega. Próxima ação: revisar o diff de segurança do vídeo, executar CI na branch quando publicada e ensaiar MP4 antes da apresentação. Não confundir a CI da base com validação remota desta atualização.

## Estado vigente — validação de 23/11/2026

A PR #38 foi aprovada e integrada em 19/11/2026 (`68d08dd`); a main consultada já inclui a PR #41 (`d3f4725`, retirada de denúncias de publicações ocultadas da fila). As pendências de aprovação da #38 citadas no histórico abaixo estão encerradas.

Trabalho local em `fix/validacao-local-20261123`: corrigidos alertas npm com Angular build 22.2.5/Vite 8.3.5 e compiler-cli 22.2.3 explícito; removido arquivo acidental de diff com nome inválido no Windows; bloqueada nova comparação gratuita quando uma chave de idempotência já usada não tem cache reutilizável. Mantido o contrato de indisponibilidade existente, sem IA paga ou cobrança real.

Validação executada **localmente** com PostgreSQL 17 descartável separado: **258 testes .NET aprovados, zero pulados**; builds Angular produção/desenvolvimento; auditoria npm online sem vulnerabilidades reportadas; 14 testes de vídeo; cenários HTTP de publicação, isolamento, moderação, cache, chave repetida e quota aprovados. Teste de regressão de idempotência registrado RED antes da correção e GREEN depois. Interface conferida em desktop 1280×900 e celular 390×844, sem overflow horizontal; cadastro, publicação/salvamento, comparação simulada, consumo 1→2→3 e bloqueio da quarta comparação observados.

Comandos, limites, evidências e roteiro reproduzível: [validação local](VALIDACAO-LOCAL-2026-11-23.md). Prompt reutilizável para outros projetos: [quatro ciclos de revisão](PROMPT-REVISAO-CONTINUA.md). As novas alterações ainda dependem de PR/CI/revisão antes do merge; não houve deploy. Docker segue indisponível e os feeds reais não foram testados nesta rodada. Alterações paralelas em documentos de aulas/Kubernetes foram preservadas fora deste trabalho.

## Decisão 0006 — monólito modular (17/11/2026)

Proposta do Davidy no grupo e aceita pelo Hugo: seguir com uma solução e um banco, com os módulos de negócio (Comunidade, Radar, Comparação, Planos e quota) em projetos e comunicação entre eles só por interface. Registro em `docs/decisoes/0006-monolito-modular.md`. **Nenhuma mudança estrutural até a entrega**; a extração da Comunidade do `Program.cs` e a regra entre módulos no `ArchitectureTests` ficam para depois da banca, nessa ordem. Mudança só de documentação.

## Histórico: integração revisada — 17/11/2026

Estado naquela data: `integracao/revisao-2026-11-17`, PR #38. Os merges foram preparados nessa branch e aguardavam revisão. Posteriormente a PR foi aprovada e integrada em 19/11; consulte o estado vigente acima.

Incluídos, com histórico original preservado:

| Branch / PR | Motivo |
| --- | --- |
| feat/deploy-render / #33 | Reunir entregas #24–#29 que ficaram fora da main: histórias, triagem, pipeline, planos/quota persistente e comparação simulada com tela conectada |
| feat/catalogo-fontes / #36 | Catálogo único de 30 fontes com curadoria registrada e correção do agrupamento entre veículos |
| test/api-cenarios-b08 / #32 | Cobrir filtros, paginação, proteção de origem e moderação com administrador |
| feat/video-hyperframes / #30 | Material de apresentação, proposta, tour do código e gerador de vídeo |

Conflito resolvido em `.github/workflows/ci.yml`, preservando a integração PostgreSQL e o fluxo administrativo. A junção do catálogo com o vídeo revelou teste de tour desatualizado: referências de `StoryClustering.cs` corrigidas e roteiro/documento/composição regenerados. Corrigida também a identificação de execução direta de `tour.mjs` no Windows usando `pathToFileURL`.

Validação do commit `933502d`: localmente, 246 testes .NET passaram e 12 de PostgreSQL foram pulados sem conexão; build Angular de produção e 14 testes do gerador passaram. Na [CI da PR](https://github.com/Pemonition/Coreletter/actions/runs/44131754030), **258 testes .NET passaram, zero pulados**, com PostgreSQL real descartável; passaram também cenários HTTP de administrador/origem, frontend, vídeo e imagens/execução em container. Não houve inspeção visual, renderização MP4 nem deploy nesta revisão. Docker local indisponível; tentativa de PostgreSQL local separado não iniciou (diretório pg_notify ausente), sem alterar o serviço PostgreSQL existente.

`npm ci` reportou dois alertas (um baixo e um alto); a consulta detalhada online falhou por rede/DNS. Auditoria offline sem alertas não substitui a consulta online. Não foi aplicado `audit fix --force` nem alterado lockfile.

Ficaram fora: `Pemonition` / #31 e `feat/questionnaire-collection` (coleta privada precisa conciliar catálogo, esquema e rotas com o coletor integrado); `claude/frontend-visual` (carrega esse radar); `feat/arquitetura-hexagonal-k8s` / #37 (conflitos e refatoração baseada numa estrutura anterior); `feat/componentizar-main-ts` (parte da tela antiga, perde o fluxo de histórias e introduz comparação sem endpoint conectado). Nenhuma branch foi apagada.

Pendências registradas naquela data: aprovação da #38 (concluída em 19/11) e revisão de leases/versão e idempotência antes de provedor pago. A correção de chave repetida em 23/11 fecha um desses caminhos; expiração, cancelamento e coordenação com provedor real ainda exigem revisão própria. A comparação permanece simulada. Nenhuma cobrança ou IA real foi ativada.

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
