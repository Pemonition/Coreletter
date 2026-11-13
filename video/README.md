# Vídeos com HyperFrames

Esta pasta transforma um **roteiro em JSON** num **vídeo MP4** com a marca do Coreletter.

Usos planejados (issue #20):

- **Vídeo da equipe:** "Como o Coreletter funciona" (59 s, `roteiros/como-funciona.json`).
- **Proposta:** "De pessoas para pessoas, sem algoritmo" (1 min, `roteiros/proposta.json`): os 4 compromissos e quanto cumprimos (`docs/PROPOSTA.md`).
- **Tour pelo código:** "Por dentro do Coreletter" (2min42, `roteiros/tour-do-codigo.json`), cada parte com o trecho real. Gerado por `gerador/tour.mjs` a partir de `roteiros/tour-do-codigo.fonte.json`; o mesmo script escreve `docs/TOUR.md`. Se o código mudar, um teste avisa: rode `node video/gerador/tour.mjs --docs docs/TOUR.md` e gere de novo.
- **Abertura do TCC** e **artigo da plataforma:** o mesmo vídeo, ou um corte dele.
- **Tutoriais para o usuário:** como comparar fontes, planos etc.
- **Produto:** o vídeo-resumo de cada história, gerado pelo pipeline (auditor → roteirista → vídeo, `docs/decisoes/0003`).

## O que é o HyperFrames e por que ele

[HyperFrames](https://github.com/heygen-com/hyperframes) (HeyGen, licença Apache 2.0) renderiza **uma página HTML animada** num MP4. Cada cena é um elemento HTML com início e duração, e as animações usam GSAP.

| Alternativa | Por que não |
|---|---|
| Editor de vídeo (CapCut, Premiere) | Trabalho manual a cada vídeo; não escala para "um vídeo por história". |
| Gerador de vídeo por IA (texto → vídeo) | Caro por vídeo e imprevisível: pode inventar imagem ou texto. Para notícia, isso é risco. |
| **HTML + HyperFrames** | Texto exato (o que está no roteiro é o que aparece), marca consistente, custo zero por vídeo, roda local. A equipe já sabe HTML/CSS. |

## Como funciona

```
roteiros/<id>.json  ──►  gerador/gerar.mjs  ──►  composicoes/<id>/index.html  ──►  npx hyperframes render  ──►  MP4
   (o que dizer)        (valida + marca)          (HTML + GSAP)                  (no seu PC)
```

- **`gerador/lib.mjs`:**
  - `validarRoteiro` lista todos os erros de uma vez (limites de texto, tipos de cena, duração total até 3 min);
  - `composicao` monta o HTML;
  - `escapar` impede que texto vire código. O roteiro pode vir de IA, então isso é segurança, não detalhe;
  - `roteiroDeComparacao` cria um roteiro básico a partir de uma comparação da API (contrato v1).
- **`gerador/gerar.mjs`:** linha de comando, sem dependências (só Node 22+).
- **`composicoes/`:** saída gerada. **Não edite à mão:** edite o roteiro e gere de novo. Um teste na CI confere que a composição commitada bate com o roteiro.
- **`marca/`:** DM Sans (licença OFL, arquivo junto) e GSAP. O HyperFrames exige fonte e scripts dentro do projeto: nada vem da internet na hora do render.

## Passo a passo no Windows (PowerShell)

Pré-requisitos: Node 22+ e o HyperFrames desktop instalado (ele traz o FFmpeg). Versões fixadas em `video/package.json` e no lockfile: **HyperFrames 0.9.40** e **GSAP 3.14.2**. É a combinação já validada no PC do Hugo (PR #31).

1. Entre na pasta: `cd video`
2. Instale as versões fixadas: `npm ci --ignore-scripts`
3. Gere a composição: `npm run gerar`. Ou `node gerador/gerar.mjs roteiros/<id>.json` para outro roteiro.
4. Confira as regras: `npm run check`. A meta é **0 erros**. Avisos de "separar em subcomposições" são sugestão de organização, não erro.
5. Veja no navegador: `npm run preview`
6. Renderize: `pwsh -File Render-Local.ps1`. O script usa o FFmpeg do app, roda `check` antes e grava `renders/como-funciona.mp4`.
7. Coloque o MP4 no Drive da equipe. **Não commite MP4 no Git**: arquivo grande e regenerável (`renders/` está no `.gitignore`).

**Editou no HyperFrames Studio?** O gerador percebe (assinatura no fim do `index.html`) e **não sobrescreve** a sua edição. Leve a mudança para o roteiro ou para o gerador e gere de novo com `--forcar`.

**Por que o GSAP é arquivo local e não CDN:** com o CDN, o `hyperframes check` não encontra a timeline. Isso foi verificado na PR #31. O arquivo fica em `marca/gsap.min.js`, e um teste confere que a versão bate com o lockfile.

## Escrever um roteiro

```json
{
  "versao": 1,
  "id": "tutorial-comparar",
  "titulo": "Como comparar fontes",
  "formato": { "largura": 1920, "altura": 1080 },
  "cenas": [
    { "tipo": "abertura", "duracao": 5, "titulo": "Comparar fontes", "texto": "Em 3 passos." },
    { "tipo": "etapa", "duracao": 7, "numero": 1, "titulo": "Abra Histórias e fontes", "texto": "...", "itens": ["...", "..."] },
    { "tipo": "comparacao", "duracao": 10, "titulo": "O que cada fonte diz",
      "afirmacoes": [{ "status": "divergente", "texto": "...", "fontes": ["Veículo A", "Veículo B"] }] },
    { "tipo": "texto", "duracao": 6, "titulo": "...", "texto": "..." },
    { "tipo": "fechamento", "duracao": 5, "titulo": "...", "texto": "..." }
  ]
}
```

| Campo | Regra |
|---|---|
| `id` | minúsculas, números e hífen; vira o nome da pasta |
| `formato` | 1920x1080 (horizontal), 1080x1920 (vertical, Reels/Shorts) ou 1080x1080 |
| `tipo` | `abertura`, `texto`, `etapa` (com `numero` 1–9), `comparacao` (com `afirmacoes`), `codigo` (com `arquivo`, `linha`, `linguagem` e `codigo`: até 16 linhas na tela), `fechamento` |
| `duracao` | 2 a 30 s por cena; total até 180 s |
| `titulo` / `texto` | até 70 / 180 caracteres, uma linha (a quebra é automática) |
| `itens` | até 4, com até 70 caracteres cada |
| `afirmacoes` | até 4; `status` igual ao contrato da comparação: `concordante`, `divergente`, `unica_fonte`, `evidencia_insuficiente` |

**Regra editorial:** o vídeo não diz o que é verdade. Mostra onde as fontes concordam e divergem, como a tela (aceite da B06).

Vídeo de uma história real, a partir da API:

```powershell
curl http://localhost:5080/api/stories/<id>/comparison -o comparacao.json
node video/gerador/gerar.mjs --de-comparacao comparacao.json --id caixa-vazamento
```

Isso grava `roteiros/caixa-vazamento.json` (revise o texto) e a composição.

## Regras do HyperFrames que o gerador já segue

Fonte: documentação oficial (skill `hyperframes-core`). Os testes em `gerador/lib.test.mjs` conferem cada uma:

- Raiz `<div data-composition-id>` direto no `<body>`, **sem `<template>`** (com `<template>`, nada aparece).
- `data-duration` da raiz = soma das cenas; nenhuma cena passa do fim.
- Cada cena é `.clip` com `data-start` e `data-duration`. A animação vai nos **filhos** do `.clip`, nunca no próprio clip.
- Uma timeline `gsap.timeline({ paused: true })` registrada em `window.__timelines["<id>"]`, com o mesmo id da raiz.
- Nada aleatório nem dependente de relógio (`Math.random`, `Date`): o render tem que sair igual toda vez.
- Fonte com `@font-face` apontando para arquivo local; sem `<br>`.

## Testes

```powershell
node --test video/gerador/*.test.mjs
```

Também roda na CI (job `video`).

## Próximos passos

Estes são sugestões. Quem pegar, melhore à vontade e registre na issue #20.

- [ ] **Narração:** voz gerada (ElevenLabs, ou a voz da própria equipe) + legenda queimada. O HyperFrames aceita `<audio id="..." data-start>`.
- [ ] **Adaptador no pipeline:** etapa `video` do Worker (`--etapa video`) que lê o roteiro (#19), chama o gerador e grava o caminho do MP4. Porta `IGeradorVideo` em `RadarPlural.Pipeline/Ports.cs`.
- [ ] **Cenas novas:** gráfico de cobertura por veículo, captura de tela do produto (tutoriais), logos dos veículos (conferir direito de uso).
- [ ] **Vertical 1080x1920** revisado para Reels/Shorts (o gerador já aceita, falta ajuste fino de layout).
- [ ] **Abertura do TCC:** roteiro próprio, de 20 a 30 s, com o nome do grupo e dos integrantes.
