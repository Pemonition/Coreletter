using System.Globalization;
using System.Text.RegularExpressions;
using RadarPlural.Ingestion;
using RadarPlural.Pipeline;
using RadarPlural.Pipeline.Comparison;

namespace RadarPlural.Infrastructure;

// Auditor simulado (B05): regras determinísticas, sem IA, para a demo funcionar sem chave e sem internet.
// Toda citação é um trecho literal do título ou resumo, então sempre passa no ContractValidator.
//   1. Concordância: manchetes que dividem 2+ palavras relevantes tratam do mesmo fato.
//   2. Divergência: o mesmo substantivo com números diferentes entre fontes ("2 milhões de clientes" × "200 mil clientes").
//   3. Única fonte: trecho do resumo que quase não tem palavras em comum com as outras fontes.
//   Sem concordância: "evidencia_insuficiente" (textos não comparáveis).
public sealed partial class SimulatedAuditor(TimeProvider clock) : IAuditor
{
    public string Name => "simulado";
    public bool Simulated => true;

    [GeneratedRegex(@"(?<num>\d+(?:[.,]\d+)?)\s*(?<mult>mil|milh(?:ão|ões)|bilh(?:ão|ões)|%)?\s+(?:de\s+)?(?<noun>\p{L}{3,})", RegexOptions.IgnoreCase)]
    private static partial Regex Quantity();

    // Trechos: corta em pontuação e em " e " / " mas " (uma frase costuma juntar um ponto comum e um exclusivo).
    [GeneratedRegex(@"[.;!?,]|\s+(?:e|mas|porém)\s+", RegexOptions.IgnoreCase)]
    private static partial Regex ClauseBreak();

    public Task<ComparisonResult> AuditAsync(StorySnapshot story, CancellationToken ct)
    {
        var items = story.Items.OrderBy(i => i.PublishedAt).ThenBy(i => i.ItemId).ToList();
        var claims = new List<Claim>();
        string NextId() => $"k{claims.Count + 1}";

        // 1. Concordância
        var titleTokens = items.ToDictionary(i => i.ItemId, i => HeadlineTokens.Of(i.Title));
        var agreeing = items.Where(a => items.Any(b => b.ItemId != a.ItemId && titleTokens[a.ItemId].Intersect(titleTokens[b.ItemId]).Count() >= 2)).ToList();
        if (agreeing.Count >= 2)
            claims.Add(new Claim(NextId(), $"{agreeing.Count} fontes noticiam o mesmo acontecimento.", ClaimStatus.Concordante,
                agreeing.Select(i => i.ItemId).ToList(), [], agreeing.Select(i => new Evidence(i.ItemId, i.Title)).ToList()));

        // 2. Divergência numérica
        var quantities = items.SelectMany(i => Quantity().Matches(i.Title + ". " + i.Summary).Select(m => (Item: i, Match: m))).ToList();
        foreach (var group in quantities.GroupBy(q => Singular(HeadlineTokens.Fold(q.Match.Groups["noun"].Value))))
        {
            var byValue = group.GroupBy(q => Value(q.Match)).ToList();
            if (byValue.Count < 2 || group.Select(q => q.Item.ItemId).Distinct().Count() < 2) continue;
            var first = byValue[0].Select(q => q.Item.ItemId).Distinct().ToList();
            var others = byValue.Skip(1).SelectMany(g => g.Select(q => q.Item.ItemId)).Distinct().Except(first).ToList();
            if (others.Count == 0) continue;
            claims.Add(new Claim(NextId(), $"As fontes informam números diferentes para \"{group.First().Match.Groups["noun"].Value}\".", ClaimStatus.Divergente,
                first, others, group.Select(q => new Evidence(q.Item.ItemId, q.Match.Value.Trim())).DistinctBy(e => (e.ItemId, e.Quote)).ToList()));
        }

        // 3. Única fonte (só faz sentido quando as fontes tratam do mesmo fato)
        if (agreeing.Count >= 2)
        {
            foreach (var item in items)
            {
                var others = items.Where(o => o.ItemId != item.ItemId).SelectMany(o => HeadlineTokens.Of(o.Title + " " + o.Summary)).ToHashSet();
                foreach (var clause in ClauseBreak().Split(item.Summary))
                {
                    var tokens = HeadlineTokens.Of(clause);
                    if (tokens.Count < 3 || tokens.Count(others.Contains) > 1) continue;
                    var quote = clause.Trim();
                    if (quote.Length > ContractValidator.MaxQuoteLength) quote = quote[..ContractValidator.MaxQuoteLength].TrimEnd();
                    claims.Add(new Claim(NextId(), $"Só {item.SourceName} menciona este ponto.", ClaimStatus.UnicaFonte, [item.ItemId], [], [new Evidence(item.ItemId, quote)]));
                    break; // no máximo um ponto por fonte, para não poluir
                }
            }
        }
        else
        {
            claims.Add(new Claim(NextId(), "Os textos disponíveis não trazem afirmações factuais comparáveis.", ClaimStatus.EvidenciaInsuficiente, [], [], []));
        }

        var sources = items.Select(i => new ComparisonSource(i.ItemId, i.SourceId, i.SourceName, i.Url, i.Title, i.Summary, i.PublishedAt)).ToList();
        return Task.FromResult(new ComparisonResult(ContractValidator.Version, story.StoryId, Name, true, clock.GetUtcNow(), sources, claims,
            new ComparisonSummary(0, 0, 0, 0), ["Resultado simulado: gerado por regras, sem inteligência artificial."]));
    }

    private static string Singular(string t) => t.Length > 4 && t.EndsWith('s') ? t[..^1] : t;

    private static decimal Value(Match m)
    {
        var n = decimal.Parse(m.Groups["num"].Value.Replace(',', '.'), CultureInfo.InvariantCulture);
        var mult = HeadlineTokens.Fold(m.Groups["mult"].Value);
        return mult switch
        {
            "mil" => n * 1_000m,
            "milhao" or "milhoes" => n * 1_000_000m,
            "bilhao" or "bilhoes" => n * 1_000_000_000m,
            _ => n
        };
    }
}
