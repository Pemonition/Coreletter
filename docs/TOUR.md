# Tour pelo código

<!-- GERADO por video/gerador/tour.mjs a partir de video/roteiros/tour-do-codigo.fonte.json. Edite a fonte e gere de novo. -->

Coleta, triagem, agrupamento, fila, comparação, plano e tela. Embaixo de tudo: arquitetura limpa, testes e CI. Cada seção mostra **onde** está o código, **por que** ele é assim e o trecho real.

O vídeo "Por dentro do Coreletter" (`video/roteiros/tour-do-codigo.json`) usa exatamente estes trechos.

| # | Parte | Arquivo |
|---|---|---|
| 1 | Coleta segura | `backend/RadarPlural.Ingestion/SafeFeedFetcher.cs` |
| 2 | Triagem por regras, sem IA | `backend/RadarPlural.Ingestion/Triage.cs` |
| 3 | A mesma notícia em vários veículos | `backend/RadarPlural.Ingestion/StoryClustering.cs` |
| 4 | Fila que roda em várias réplicas | `backend/RadarPlural.Infrastructure/PgStoryQueue.cs` |
| 5 | Sem veredito de verdade | `backend/RadarPlural.Pipeline/Comparison/Contract.cs` |
| 6 | Regra de rentabilidade (RN02) | `backend/RadarPlural.Domain/Billing/Plans.cs` |
| 7 | Quota sem corrida (RN03) | `backend/RadarPlural.Infrastructure/PgUsageLedger.cs` |
| 8 | Arquitetura limpa vigiada por teste | `backend/RadarPlural.Tests/ArchitectureTests.cs` |
| 9 | A tela não cobra duas vezes | `frontend/src/main.ts` |
| 10 | Escala com teto de custo | `deploy/k8s/10-api.yaml` |
| 11 | Este vídeo também é código | `video/gerador/lib.mjs` |

## 1. Coleta segura

O IP é conferido na hora da conexão, depois do DNS. Nenhum site da lista consegue desviar a coleta para a nossa rede interna (ataque SSRF).

`backend/RadarPlural.Ingestion/SafeFeedFetcher.cs`, linhas 85–89:

```csharp
ConnectCallback = async (context, ct) =>
{
    var addresses = await Dns.GetHostAddressesAsync(context.DnsEndPoint.Host, ct);
    if (addresses.Length == 0 || !addresses.All(UrlGuard.IsPublicAddress))
        throw new HttpRequestException($"{context.DnsEndPoint.Host} resolve para endereço não público; bloqueado pela política de ingestão.");
```

## 2. Triagem por regras, sem IA

Regras simples e testáveis: sai promoção, resumo do dia e notícia velha. Título caça-clique não sai, só ganha um aviso.

`backend/RadarPlural.Ingestion/Triage.cs`, linhas 32–42:

```csharp
if (source.ExcludeUrlContains?.Any(p => path.Contains(p, StringComparison.OrdinalIgnoreCase)) == true)
    return Discard(ReasonPromoUrl);

var title = HeadlineTokens.Fold(entry.Title);
if (Promotional().IsMatch(title)) return Discard(ReasonPromoTitle);
if (Roundup().IsMatch(title)) return Discard(ReasonRoundup);
if (entry.PublishedAt is { } published && now - published > MaxAge) return Discard(ReasonOld);

var flags = new List<string>();
if (Clickbait().IsMatch(title) || MostlyUppercase(entry.Title)) flags.Add(FlagClickbait);
return new TriageResult(true, null, flags);
```

## 3. A mesma notícia em vários veículos

Duas manchetes são a mesma história quando dividem 3 ou mais palavras relevantes e pelo menos uma delas é nome próprio.

`backend/RadarPlural.Ingestion/StoryClustering.cs`, linhas 109–113:

```csharp
public static bool SameStory(IReadOnlySet<string> wordsA, IReadOnlySet<string> namesA, IReadOnlySet<string> wordsB, IReadOnlySet<string> namesB)
{
    var shared = wordsA.Intersect(wordsB).ToList();
    return shared.Count >= MinSharedWords && shared.Any(t => namesA.Contains(t) || namesB.Contains(t));
}
```

## 4. Fila que roda em várias réplicas

SKIP LOCKED: cada réplica pega histórias diferentes sem esperar a outra. O lease devolve a história à fila se uma réplica cair.

`backend/RadarPlural.Infrastructure/PgStoryQueue.cs`, linhas 15–24:

```sql
WITH picked AS (
    SELECT "Id" FROM "Stories"
    WHERE "Status" = $1 AND "SourceCount" >= $2 AND ("LeaseUntil" IS NULL OR "LeaseUntil" <= $3)
    ORDER BY "LastPublishedAt" DESC, "Id"
    LIMIT $4
    FOR UPDATE SKIP LOCKED
)
UPDATE "Stories" s
SET "LeaseOwner" = $5, "LeaseUntil" = $3 + $6, "Attempts" = s."Attempts" + 1
FROM picked WHERE s."Id" = picked."Id"
```

## 5. Sem veredito de verdade

Toda resposta do auditor passa por aqui: afirmação com 'verdadeiro' ou 'falso' é removida, e citação que não existe no texto da fonte é descartada.

`backend/RadarPlural.Pipeline/Comparison/Contract.cs`, linhas 45–56:

```csharp
public static ComparisonResult Sanitize(ComparisonResult raw)
{
    var text = raw.Sources.GroupBy(x => x.ItemId).ToDictionary(g => g.Key, g => Norm(g.First().Title + " " + g.First().Summary));
    var claims = new List<Claim>();
    var removed = 0;
    foreach (var c in raw.Claims)
    {
        if (Verdict().IsMatch(c.Text)) { removed++; continue; }
        var evidence = c.Evidence
            .Where(e => !string.IsNullOrWhiteSpace(e.Quote) && e.Quote.Length <= MaxQuoteLength && text.TryGetValue(e.ItemId, out var t) && t.Contains(Norm(e.Quote)))
            .Select(e => e with { Quote = e.Quote.Trim() })
            .ToList();
```

## 6. Regra de rentabilidade (RN02)

Preço mínimo = custo no pior caso ÷ (1 − 25%), arredondado para cima. A API não inicia se algum plano der prejuízo.

`backend/RadarPlural.Domain/Billing/Plans.cs`, linhas 27–32:

```csharp
// Menor preço que mantém a margem mínima, arredondado PARA CIMA no centavo (arredondar para baixo furaria a regra).
public static decimal MinimumPrice(Plan plan, CostAssumptions c)
{
    if (c.MinimumMargin is < 0 or >= 1) throw new ArgumentOutOfRangeException(nameof(c), "Margem mínima deve estar entre 0 e 1.");
    var exact = MaxVariableCost(plan with { MonthlyPrice = 1m }, c) / (1 - c.MinimumMargin);
    return Math.Ceiling(exact * 100m) / 100m;
```

## 7. Quota sem corrida (RN03)

Conferir 'usado < quota' e somar 1 é um comando só no banco. Com 50 pedidos ao mesmo tempo e quota 10, passam exatamente 10.

`backend/RadarPlural.Infrastructure/PgUsageLedger.cs`, linhas 39–44:

```sql
// 2) Incrementa só se ainda houver quota. Sem linha de retorno = quota cheia: desfaz a reserva.
await using (var bump = new NpgsqlCommand("""
    INSERT INTO "UsageMonths" ("MemberId", "Month", "Used") VALUES ($1, $2, 1)
    ON CONFLICT ("MemberId", "Month") DO UPDATE SET "Used" = "UsageMonths"."Used" + 1
    WHERE "UsageMonths"."Used" < $3
    RETURNING "Used"
```

## 8. Arquitetura limpa vigiada por teste

Se a camada de aplicação passar a depender de banco ou de web, este teste falha. A regra da aula vira código, não só desenho.

`backend/RadarPlural.Tests/ArchitectureTests.cs`, linhas 15–22:

```csharp
[Fact]
public void ApplicationLayerDependsOnlyOnTheDomain()
{
    // Aplicação → Domínio é a direção certa (camada de fora conhece a de dentro). O resto é proibido.
    var refs = References(typeof(RadarPlural.Pipeline.StageRunner));
    Assert.DoesNotContain(refs, r => r.StartsWith("RadarPlural.") && r != "RadarPlural.Domain");
    Assert.DoesNotContain(refs, r => Frameworks.Any(f => r.StartsWith(f)));
}
```

## 9. A tela não cobra duas vezes

Primeiro usa a comparação pública, que não gasta quota. Se precisar de uma nova, manda Idempotency-Key: clique duplo não cobra em dobro.

`frontend/src/main.ts`, linhas 245–258:

```ts
try {
  comparison = (await this.api<{ comparison: Comparison }>(`/stories/${story.id}/comparison`)).comparison;
} catch {}
if (!comparison) {
  if (!this.user()) {
    this.authOpen = true;
    this.notice.set("Entre na sua conta para pedir uma comparação.");
    return;
  }
  const result = await this.api<{ comparison: Comparison; usage: { units: number; fromCache: boolean } }>("/comparisons", {
    method: "POST",
    headers: { "Idempotency-Key": crypto.randomUUID() },
    body: JSON.stringify({ storyId: story.id }),
  });
```

## 10. Escala com teto de custo

De 2 a 5 réplicas da API conforme a CPU. O máximo é o teto: um teste lê estes arquivos e falha se o pior caso passar de R$ 800 por mês.

`deploy/k8s/10-api.yaml`, linhas 81–87:

```yaml
minReplicas: 2
maxReplicas: 5
metrics:
  - type: Resource
    resource:
      name: cpu
      target: { type: Utilization, averageUtilization: 70 }
```

## 11. Este vídeo também é código

Ele saiu de um roteiro em JSON. Todo texto passa por escapar(): o roteiro pode vir de IA e não pode virar código na página.

`video/gerador/lib.mjs`, linhas 39–46:

```js
export function escapar(valor) {
  return String(valor)
    .replaceAll("&", "&amp;")
    .replaceAll("<", "&lt;")
    .replaceAll(">", "&gt;")
    .replaceAll('"', "&quot;")
    .replaceAll("'", "&#39;");
}
```
