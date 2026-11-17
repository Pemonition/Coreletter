# Onde ver e como mostrar o projeto

Estado em 12/11/2026.

## O que existe para mostrar

| O quê | Onde | Para quê |
|---|---|---|
| **Demo no ar** | https://coreletter.onrender.com | O produto rodando. Hoje sem notícias (ver abaixo) |
| **Mapa do Coreletter** | Artefato no claude.ai (o Hugo compartilha o link) | Visão global da proposta e de cada parte, com o código |
| **Vídeo "De pessoas para pessoas, sem algoritmo"** (1 min) | `video/roteiros/proposta.json` | Abertura da apresentação: a proposta e como cumprimos |
| **Vídeo "Por dentro do Coreletter"** (2min42) | `video/roteiros/tour-do-codigo.json` | Cada parte do projeto com o trecho real do código |
| **Vídeo "Como o Coreletter funciona"** (59 s) | `video/roteiros/como-funciona.json` | Explicação curta para usuário e equipe |
| **Tour pelo código, em texto** | `docs/TOUR.md` | Os mesmos trechos do vídeo, para ler no GitHub |
| **Avaliação da proposta** | `docs/PROPOSTA.md` | O que cumpre, o que falta e o plano |
| **Painel de entrega** | Artefato "Coreletter — Fechamento" | Requisitos do Regulamento, PRs e quem faz o quê |

## Deixar a demo com notícias (Hugo, uma vez)

A demo está no ar, mas vazia: o workflow "Coletar notícias" nunca rodou contra o banco do Render.

1. No Render, abra o banco `coreletter-db` e copie a **External Database URL**.
2. No GitHub, vá em Settings → Secrets and variables → Actions → **New repository secret**. Nome: `INGESTAO_DATABASE_URL`. Valor: a URL copiada + `?sslmode=require`.
3. Em Actions → **Coletar notícias** → **Run workflow**. Em cerca de 1 minuto aparecem ~200 notícias e ~10 histórias com 2 ou mais fontes.
4. Para ter comparações prontas sem login: rode a etapa de auditoria uma vez, apontando para o mesmo banco: `dotnet run --project backend/RadarPlural.Worker -- --etapa auditoria --lote 20`.

**Atenção:** o banco gratuito do Render expira por volta de 26/11/2026. Recrie ou exporte antes da banca.

## Rodar local (plano B da apresentação)

Siga o `README.md` da raiz: PostgreSQL, depois a API, depois o coletor (`dotnet run --project backend/RadarPlural.Worker`), depois a auditoria (`-- --etapa auditoria --lote 20`) e o frontend (`npm start`). Assim você tem dados reais mesmo sem internet estável no Render.

## Renderizar os vídeos (no PC do Hugo)

Na pasta `video`:
1. `npm ci --ignore-scripts`
2. `npm run gerar` (ou `node gerador/gerar.mjs roteiros/<id>.json` para outro roteiro)
3. Para cada vídeo:
   - `npx hyperframes check composicoes/<id> --json`
   - `npx hyperframes render composicoes/<id> --output renders/<id>.mp4`
4. Para "Como funciona", basta `pwsh -File Render-Local.ps1`, que já faz o check e o render.

Os `<id>` são `proposta`, `tour-do-codigo` e `como-funciona`.

Detalhes e regras do HyperFrames: `video/README.md`.

## Roteiro sugerido para a banca (10 a 12 min)

1. **Vídeo da proposta** (1 min): o problema do feed algorítmico e os 4 compromissos.
2. **Demo ao vivo** (4 min):
   - histórias com várias fontes;
   - comparar uma delas e mostrar os trechos e os links;
   - conta Grátis chegando a 3 de 3 comparações;
   - feed da comunidade.
3. **Mapa do Coreletter** (3 min): abrir 2 ou 3 partes e mostrar o código (feed cronológico, validador do contrato, regra de rentabilidade).
4. **O que falta** (1 min): a seção "Para cumprir a proposta inteira" do mapa.
5. **Perguntas.** Plano B: o vídeo "Por dentro do Coreletter" substitui a demo se a rede falhar.
