## Frontend espacial — 12/11/2026

Validação: 6 testes frontend aprovados, build produção aprovado (235,92 kB inicial), git diff --check aprovado. Inspeção visual no navegador ainda pendente; não equivale a teste de responsividade executado.

Branch Pemonition: novo destaque visual com geometria CSS em perspectiva, esfera iluminada, anéis e cartões; refinamento de cards, foco visível, link para pular ao conteúdo e navegação móvel com quebra de linha. Sem modelo Blender, WebGL ou dependência nova. A animação é finita e não roda quando prefers-reduced-motion está ativo. As branches de histórias dos colegas não foram alteradas. Esta evolução visual aplica-se ao frontend preservado nesta branch; a conciliação com feat/tela-historias continua pendente.

Evolução sugerida: Lindomar revisar layout real em celular/teclado e integrar ao frontend de histórias; Hugo definir modelo artístico; exportar GLB do Blender e só então avaliar carregamento sob demanda, fallback estático, peso e suporte a WebGL antes de incorporar. Nenhum modelo Blender foi criado nesta sessão.

Vídeo demonstrativo renderizado em 12/11/2026: 26s Full HD sem áudio, em video/output-final/coreletter.mp4. PR rascunho #31 publicada com comentário para revisão dos colegas; main e branches remotas existentes preservadas.

## Continuidade — 12/11/2026, branch Pemonition

Alterações locais de questionário/coleta preservadas em dee2258. Validação desta sessão: 55 testes backend aprovados, 3 de PostgreSQL pulados; 6 frontend aprovados e build desenvolvimento aprovado. Não confundir com validação PostgreSQL histórica abaixo.

Nova integração local demonstrativa HyperFrames em video/ e docs/HYPERFRAMES.md. Sugestões e comentário para colegas em docs/REVISAO-EQUIPE-PEMONITION.md. Branches remotas avançadas foram lidas e preservadas; não foram integradas porque mudam contratos/migrações em comum. Claude Code deve retomar conferindo Git e esta documentação; não há transferência de login entre ferramentas.

# Estado da base automatizada

## Incremento local — 27/10/2026: questionário e coleta

Prevalece sobre os trechos históricos abaixo quanto à coleta: implementados questionário autenticado, preferências PostgreSQL, worker periódico, fontes RSS/Atom controladas, filtros, resultados privados, pausa, deduplicação e exportação JSON para análise manual. Sem integração real de IA, envios de newsletter, cobrança ou coleta real habilitada. Catálogo inicial vazio e `Collection:Enabled=false`.

Política e ativação: `docs/decisoes/0001-coleta-por-questionario.md`. Contrato em `backend/API.md`. Não há fetch de URLs arbitrárias do cliente. Histórico preservado. Novo esquema aditivo aplicado apenas ao PostgreSQL descartável nesta validação; nenhum banco do curso foi alterado.

Validação executada: 58 testes backend aprovados, incluindo três com PostgreSQL 17 real descartável; 6 testes frontend aprovados e builds Angular desenvolvimento/produção aprovados. `scripts/Test-Collections.ps1` aprovado por HTTP na API local com contas fictícias: 401, conta nova, salvar/alterar questionário pausado, isolamento, bloqueio de URL fora do catálogo, coleta global desligada, export e logout. Dois especialistas auxiliaram implementação frontend e revisão estática. CI foi atualizada para executar estes testes, mas ainda não foi disparada remotamente.

Limites: testes de rede usaram respostas HTTP controladas; não houve coleta de uma fonte pública nem inspeção visual no navegador. A transação do worker mantém lock durante até três downloads limitados e não é uma fila de grande escala. Para demonstrar coleta real, o responsável precisa configurar e habilitar uma fonte permitida, iniciar API/banco, preencher o questionário e ativar o radar. Material acadêmico ainda requer entendimento e revisão da equipe antes do merge.

Esta implementação foi preparada inicialmente em cópia de trabalho isolada do Life Project, preservando o original durante testes. Não publicada no GitHub. Prospeção comercial fica para depois do projeto do curso, conforme Hugo.

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

## Próxima sessão

1. Aceitar convites, conferir CI e executar B01 com PostgreSQL na máquina dos integrantes.
2. Corrigir os problemas observados antes de novas funcionalidades.
3. Hugo e Davidy definem contrato de comparação B04; Lindomar e Henrique executam tarefas pequenas do backlog, com revisão.
4. Registrar RED/GREEN nas próximas regras para evidenciar TDD; não inventar evidência retroativa.

Nunca colocar chaves ou dados privados neste arquivo. Atualizar o estado quando uma limitação for resolvida.

## Frontend visual: inspeção e correções (Claude Code), 12/11/2026 à noite

Branch `claude/frontend-visual`, criada a partir da `Pemonition` em db8fb96. Combinado com o Hugo: Claude edita só blocos novos e o destaque; o Codex fica com `main.ts` (exceto a linha de import), `collection-radar.*`, os testes existentes e o backend.

**Inspeção no navegador** (Chromium/Playwright; API + PostgreSQL locais com dados fictícios; 1440, 1024, 390 e 360 px, com e sem login):
- **Aprovado antes das mudanças:** sem rolagem horizontal; ordem de Tab lógica e foco visível nos 16 primeiros elementos; "Pular para o conteúdo" leva ao `main`; animação desligada com "reduzir movimento".
- **Problemas encontrados:**
  - hero de 510 px no celular, sem nenhuma notícia na primeira tela;
  - hero completo em todas as telas;
  - 16 estilos de texto abaixo de WCAG AA (2,2:1 a 4,4:1);
  - aba ativa sem `aria-current`.

**Arquivos:**
- novos: `frontend/src/brand.ts` (nome exibido central), `frontend/src/hero.ts` (destaque, com modo compacto), `frontend/tests/brand.test.mjs`, `docs/FRONTEND-3D.md`, `docs/FRONTEND-CONCILIACAO.md`;
- alterados: `app.html` (usa `<app-brand>`/`<app-hero>` e `aria-current`), `main.ts` (1 import, commit separado), `styles.css` (regra móvel do bloco Spatial + bloco novo no fim).

**Resultado medido depois:**
- hero no celular com 267–280 px; texto sem sobreposição medido linha a linha; nada cortado;
- fora do feed, o hero vira uma faixa curta (radar no celular: o formulário subiu de ~760 para ~440 px);
- 0 textos abaixo de AA no feed e no radar;
- sem rolagem horizontal nos 4 tamanhos.

**Testes:** `npm test` com 10 aprovados (6 existentes + 4 do nome central; mutação conferida: nome fixo no template faz o teste falhar). Build de produção aprovado (239,65 kB inicial, 65,55 kB gzip). Cada um dos 5 commits compila e passa sozinho.

**Limitações:**
- inspeção em Chromium, sem Safari/Firefox nem leitor de tela real;
- a medição de sobreposição usa retângulos: na regra que inclui os anéis, a elipse inclinada gera falso positivo no tablet (conferido no olho);
- o `<title>` estático do `index.html` continua "Coreletter" no primeiro carregamento e é trocado pelo `brand.ts` em seguida;
- `collection-radar.html` ainda cita "Coreletter" num texto. Fica para o Codex usar `BRAND.nome`.

**Próximos passos:**
- Hugo/equipe: decidir o nome (trocar só em `brand.ts`) e se haverá arte no Blender (`docs/FRONTEND-3D.md`);
- conciliação com `feat/tela-historias` conforme `docs/FRONTEND-CONCILIACAO.md`;
- Lindomar: desenhar o menu móvel para 5 abas.
