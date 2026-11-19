# Contrato da comparação de fontes — v1 (B04)

Estado: proposta. Responsáveis: Hugo + Davidy. Decisão de contexto: `docs/decisoes/0003-pipeline-auditoria-video.md`.

O auditor compara **como diferentes veículos cobriram a mesma história**. Ele **não** decide o que é verdade, não dá nota de confiabilidade e não certifica neutralidade. O resultado é um mapa de afirmações com evidências citadas e links, mais as limitações.

## Entrada

`POST /api/comparisons` (autenticado)

```json
{ "storyId": "8f0c…" }
```
ou, para comparar itens escolhidos:
```json
{ "itemIds": ["a1…", "b2…", "c3…"] }
```

Cabeçalho opcional `Idempotency-Key: <uuid>`: repetir a mesma requisição devolve o mesmo resultado e **não consome quota duas vezes**.

| Regra | Valor |
|---|---|
| Itens por comparação | 2 a 5 |
| Origem dos itens | Somente `FeedItems` de fontes aprovadas (0002). **URL enviada por usuário não é aceita** |
| Texto enviado ao provedor | Título (≤ 300) + resumo (≤ 300) de cada item. Nunca a página inteira |
| Itens da mesma fonte | No máximo 1 por fonte (comparação é entre veículos) |

## Resposta `200`

```json
{
  "contractVersion": "1.0",
  "id": "c9e1…",
  "storyId": "8f0c…",
  "provider": "simulado",
  "simulated": true,
  "createdAt": "2026-11-12T18:00:00Z",
  "sources": [
    { "itemId": "a1…", "sourceId": "fonte-a", "sourceName": "Fonte A", "url": "https://…", "title": "…", "summary": "…", "publishedAt": "…" }
  ],
  "claims": [
    {
      "id": "k1",
      "text": "Afirmação resumida em linguagem neutra",
      "status": "concordante",
      "supportedBy": ["a1…", "b2…"],
      "contradictedBy": [],
      "evidence": [
        { "itemId": "a1…", "quote": "trecho literal do título ou resumo" }
      ]
    }
  ],
  "summary": { "concordantes": 1, "divergentes": 0, "unicaFonte": 0, "evidenciaInsuficiente": 0 },
  "limitations": [
    "Comparação feita sobre manchetes e resumos, não sobre o texto completo das matérias."
  ],
  "usage": { "units": 1, "fromCache": false }
}
```

### Status de cada afirmação
| Status | Quando |
|---|---|
| `concordante` | 2+ fontes sustentam, nenhuma contradiz |
| `divergente` | Fontes apresentam versões incompatíveis (números, datas, autoria, causa) |
| `unica_fonte` | Só uma fonte menciona; as outras não falam do assunto (não é contradição) |
| `evidencia_insuficiente` | Texto disponível não permite afirmar nada (ex.: só manchetes vagas) |

### Validação automática da saída do provedor (antes de responder)
Feita por código, não por IA:
1. Toda `evidence.quote` precisa aparecer **literalmente** (ignorando maiúsculas e espaços) no título ou resumo do `itemId` citado. Senão, a evidência é removida.
2. Afirmação sem nenhuma evidência válida vira `evidencia_insuficiente`.
3. `supportedBy`/`contradictedBy` só podem conter `itemId` da entrada.
4. Texto da resposta é tratado como texto puro na interface (nunca HTML).
5. Palavras proibidas no campo `text`: "verdadeiro", "falso", "fake", "confiável", "mentira". O auditor descreve, não julga.

## Erros
| HTTP | `code` | Quando | Consome quota? |
|---|---|---|---|
| 400 | `entrada_invalida` | Menos de 2 ou mais de 5 itens, item inexistente, fontes repetidas | Não |
| 401 | — | Sem login | Não |
| 403 | `quota_esgotada` | Plano sem comparações restantes no mês | Não |
| 404 | — | `storyId` inexistente | Não |
| 503 | `provedor_indisponivel` | Provedor de IA falhou ou excedeu tempo | **Não**: a reserva é liberada |

Formato: `{ "error": "mensagem", "code": "quota_esgotada" }` (mantém o `{error}` do `API.md`).

## O que conta como uso da quota
- **1 unidade por comparação concluída** com sucesso.
- A unidade é **reservada antes** de chamar o provedor e **liquidada** no sucesso ou **liberada** na falha (B07).
- Repetição com a mesma `Idempotency-Key` não consome de novo.
- **Cache:** o mesmo conjunto de itens comparado nas últimas 24 h devolve o resultado salvo com `fromCache: true`, **sem consumir quota**.

## Exemplos
Em `docs/contratos/exemplos/` (fontes fictícias, `example.org`):
- `comparacao-concordante.json`: duas fontes confirmam o mesmo fato.
- `comparacao-divergente.json`: as fontes divergem num número.
- `comparacao-insuficiente.json`: manchetes vagas, nada comparável.

São também os casos de teste do comparador simulado (B05) e da tela (B06).

## Limitações declaradas ao usuário
- Compara manchetes e resumos, não a matéria completa.
- Concordância entre veículos não prova que algo é verdade; divergência não prova que alguém mentiu.
- Fontes são escolhidas pela equipe (lista em `fontes.json`) e isso também é um recorte.
- Resultado simulado é identificado como `simulated: true` e com aviso na tela.

## Versionamento
Mudança que quebra consumidores (remover campo, mudar status) = `contractVersion` 2.0 e PR atualizando contrato, back, front e testes juntos.
