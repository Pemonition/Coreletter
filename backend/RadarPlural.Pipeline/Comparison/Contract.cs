namespace RadarPlural.Pipeline.Comparison;

// Contrato da comparação v1 (docs/contratos/comparacao-v1.md). Tipos usados por qualquer auditor (simulado ou Gemini).
public static class ClaimStatus
{
    public const string Concordante = "concordante";
    public const string Divergente = "divergente";
    public const string UnicaFonte = "unica_fonte";
    public const string EvidenciaInsuficiente = "evidencia_insuficiente";
}

public sealed record Evidence(Guid ItemId, string Quote);

public sealed record Claim(string Id, string Text, string Status, IReadOnlyList<Guid> SupportedBy, IReadOnlyList<Guid> ContradictedBy, IReadOnlyList<Evidence> Evidence);

public sealed record ComparisonSource(Guid ItemId, string SourceId, string SourceName, string Url, string Title, string Summary, DateTimeOffset? PublishedAt);

public sealed record ComparisonSummary(int Concordantes, int Divergentes, int UnicaFonte, int EvidenciaInsuficiente);

public sealed record ComparisonResult(
    string ContractVersion, Guid StoryId, string Provider, bool Simulated, DateTimeOffset CreatedAt,
    IReadOnlyList<ComparisonSource> Sources, IReadOnlyList<Claim> Claims, ComparisonSummary Summary, IReadOnlyList<string> Limitations);

// Validação em código da saída de QUALQUER auditor (contrato v1, "Validação automática"). Não confia na IA:
// citação precisa existir no texto da fonte citada; ids desconhecidos saem; afirmação sem evidência vira
// "evidencia_insuficiente"; termos de veredito (verdadeiro/falso…) não entram. O auditor descreve, não julga.
public static partial class ContractValidator
{
    public const string Version = "1.0";
    public const int MaxQuoteLength = 200;

    public static readonly IReadOnlyList<string> StandardLimitations =
    [
        "Comparação feita sobre manchetes e resumos, não sobre o texto completo das matérias.",
        "Concordância entre veículos não prova que a informação é correta; divergência não prova que alguém mentiu.",
    ];

    private static readonly HashSet<string> Statuses = [ClaimStatus.Concordante, ClaimStatus.Divergente, ClaimStatus.UnicaFonte, ClaimStatus.EvidenciaInsuficiente];

    [System.Text.RegularExpressions.GeneratedRegex(@"\b(verdadeir\w*|fals[oa]s?|fake\w*|confi[aá]ve(l|is)|mentir\w*)\b", System.Text.RegularExpressions.RegexOptions.IgnoreCase)]
    private static partial System.Text.RegularExpressions.Regex Verdict();

    private static string Norm(string s) => string.Join(' ', s.ToLowerInvariant().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

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
            var supported = c.SupportedBy.Where(text.ContainsKey).Distinct().ToList();
            var contradicted = c.ContradictedBy.Where(text.ContainsKey).Distinct().ToList();
            var status = Statuses.Contains(c.Status) ? c.Status : ClaimStatus.EvidenciaInsuficiente;
            if (evidence.Count == 0)
            {
                status = ClaimStatus.EvidenciaInsuficiente;
                supported = [];
                contradicted = [];
            }
            claims.Add(new Claim(c.Id, c.Text.Trim(), status, supported, contradicted, evidence));
        }
        var limitations = StandardLimitations.Concat(raw.Limitations).Distinct().ToList();
        if (removed > 0) limitations.Add($"{removed} afirmação(ões) removida(s) por usar termos de veredito.");
        var summary = new ComparisonSummary(
            claims.Count(x => x.Status == ClaimStatus.Concordante), claims.Count(x => x.Status == ClaimStatus.Divergente),
            claims.Count(x => x.Status == ClaimStatus.UnicaFonte), claims.Count(x => x.Status == ClaimStatus.EvidenciaInsuficiente));
        return raw with { ContractVersion = Version, Claims = claims, Summary = summary, Limitations = limitations };
    }
}
