# A proposta: de pessoas para pessoas, sem algoritmo

Hoje as pessoas querem fugir do feed que aprende o que prende a atenção e decide o que elas veem. O Coreletter junta notícias e curiosidades de **fontes escolhidas por gente**, mostra **como cada veículo contou a mesma história** e deixa **a pessoa escolher os assuntos**.

Este documento traduz a proposta em **quatro compromissos verificáveis** e confere cada um contra o código. Avaliação de 12/11/2026, nas branches `main`, `feat/deploy-render` (PR #33) e `Pemonition` (PR #31).

A versão navegável, com o detalhe de cada parte e os trechos de código, é o artefato **"Mapa do Coreletter"**. A versão em vídeo está em `video/roteiros/proposta.json`.

## Resumo

| # | Compromisso | Situação |
|---|---|---|
| 1 | Sem algoritmo de engajamento | **Cumpre** |
| 2 | Fontes escolhidas por pessoas | **Parcial** |
| 3 | Você escolhe os assuntos | **Parcial** |
| 4 | Transparência e controle | **Cumpre** (com uma lacuna) |

## 1. Sem algoritmo de engajamento: cumpre

**Evidência:**
- Feed da comunidade, histórias e salvos são ordenados pela data, do mais recente ao mais antigo (`OrderByDescending(CreatedAt)` e `OrderByDescending(LastPublishedAt)` em `backend/RadarPlural.Api/Program.cs`).
- Não existe métrica de clique, tempo de tela, curtida, recomendação ou "score" no código. Busca feita em 12/11/2026 por `click`, `engagement`, `recommend`, `track` e `score`: nada encontrado.
- Triagem e agrupamento são regras escritas e testadas, iguais para todos (`Triage.cs`, `StoryClustering.cs`). Não há personalização escondida.
- O modelo de negócio é assinatura, sem anúncio (`docs/RENTABILIDADE.md`). Ninguém ganha prendendo a atenção.

**Falta:**
- A triagem descarta cerca de 1 em cada 4 itens coletados, e o usuário não vê o que saiu nem por quê. O motivo já fica gravado (`FeedItems.DiscardReason`); falta a tela.
- A IA futura (B09, Gemini) precisa ficar presa ao contrato da comparação: comparar e citar, nunca ranquear por engajamento.

## 2. Fontes escolhidas por pessoas: parcial

**Evidência:**
- As 7 fontes do coletor foram escolhidas pela equipe e só mudam por PR revisada (`backend/RadarPlural.Worker/fontes.json`, decisão `0002`).
- Fonte que bloqueia robôs fica de fora, sem contornar (g1 e Folha).
- Na comunidade, pessoas compartilham links com comentário, e a moderação é humana.

**Falta:**
- Os critérios de "fonte de qualidade" não estão escritos (issue #23 aberta). Hoje "qualidade" é uma promessa, não um dado.
- O usuário não consegue sugerir uma fonte para o coletor.
- Pouca variedade: 6 fontes de tecnologia, 1 geral e nenhuma de curiosidades.

## 3. Você escolhe os assuntos: parcial

**Evidência:**
- O feed filtra por tema e por formato (notícia, podcast, música).
- O radar privado (branch `Pemonition`) deixa a pessoa escrever as próprias regras: tema, palavras de interesse, palavras para excluir, até 3 fontes, frequência e limite. Não usa IA nem deduz gostos (`CollectionRules.Matches`).

**Falta:**
- O radar não está na `main`: precisa virar migração (`docs/FRONTEND-CONCILIACAO.md`, na branch `claude/frontend-visual`).
- A tela de histórias não filtra por tema nem por fonte (issue #34).
- Não há "seguir tema" salvo na conta, fora do radar.

## 4. Transparência e controle: cumpre, com uma lacuna

**Evidência:**
- A comparação mostra concordâncias, divergências e o que só uma fonte diz, sempre com trecho e link.
- O validador do contrato remove afirmação com "verdadeiro", "falso" ou "fake" e descarta citação que não existe no texto da fonte (`Contract.Sanitize`).
- Uma comparação já feita é pública e não gasta quota; o uso do mês aparece na tela.

**Falta:**
- Uma página "como escolhemos o que você vê", com as fontes, as regras da triagem e quantos itens saíram em cada coleta (e por quê).

## O que fazer para cumprir a proposta inteira

| # | Ação | Compromisso | Quem |
|---|---|---|---|
| 1 | Escrever os critérios de fonte de qualidade (#23) e mostrá-los no card da fonte | 2 | Hugo e grupo |
| 2 | "Sugerir uma fonte": a pessoa propõe e a moderação aprova ou recusa com motivo | 2, 3 | Henrique e Davidy |
| 3 | Trazer o radar para a `main` e usar os temas dele como filtro das histórias ("meus temas") | 3 | Hugo, Davidy e Lindomar |
| 4 | Página "como escolhemos o que você vê" usando o `DiscardReason` | 1, 4 | Lindomar e Hugo |
| 5 | Fontes de curiosidades (divulgação científica, cultura) pela mesma política | 2 | Grupo |
| 6 | Manter a IA (B09) presa ao validador do contrato | 1, 4 | Davidy |

## Como falar disso na banca

- **A frase:** "O Coreletter não tem algoritmo de recomendação: a ordem é cronológica, as regras são públicas, e quem escolhe as fontes e os assuntos são pessoas."
- **A prova:** abrir o "Mapa do Coreletter" e mostrar o trecho do feed (ordenação por data) e o validador do contrato.
- **A honestidade:** mostrar o que ainda é parcial (critérios das fontes, radar fora da `main`) e o plano acima. Banca valoriza saber o que falta e por quê.
