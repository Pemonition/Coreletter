using System.Text.RegularExpressions;

namespace RadarPlural.Ingestion;

public sealed record TriageResult(bool Accepted, string? DiscardReason, IReadOnlyList<string> Flags);

// Triagem por regras, antes de qualquer IA (docs/decisoes/0003). Cada descarte grava o motivo.
// Regras conservadoras: na dúvida, mantém. Caça-clique só marca (a notícia pode ser real), não descarta.
public static partial class Triage
{
    public const string ReasonPromoUrl = "promocional_url";
    public const string ReasonPromoTitle = "promocional_titulo";
    public const string ReasonOld = "antiga";
    public const string ReasonRoundup = "resumo_do_dia";
    public const string FlagClickbait = "caca_clique";
    public static readonly TimeSpan MaxAge = TimeSpan.FromDays(7);

    // Aplicadas ao título minúsculo e sem acento.
    [GeneratedRegex(@"\d\s?%?\s?off\b|\bcupo(m|ns)\b|\bsem juros\b|\bpromoca(o|oes)\b|\bofertas\b|\boferta (imbativel|exclusiva|relampago)\b|vale a pena pagar|quanto vale pagar|\bo que comprar\b")]
    private static partial Regex Promotional();

    // Resumos do dia misturam várias histórias numa manchete só e atrapalham o agrupamento.
    [GeneratedRegex(@"^(hoje no|resumo do dia|giro do dia)\b")]
    private static partial Regex Roundup();

    [GeneratedRegex(@"nao vai acreditar|o impossivel aconteceu|isso muda tudo|nao pode perder|\bchocante\b|\binacreditavel\b|!{2,}")]
    private static partial Regex Clickbait();

    public static TriageResult Evaluate(FeedSource source, FeedEntry entry, DateTimeOffset now)
    {
        var path = Uri.TryCreate(entry.Url, UriKind.Absolute, out var uri) ? uri.AbsolutePath : entry.Url;
        if (source.ExcludeUrlContains?.Any(p => path.Contains(p, StringComparison.OrdinalIgnoreCase)) == true)
            return Discard(ReasonPromoUrl);

        var title = HeadlineTokens.Fold(entry.Title);
        if (Promotional().IsMatch(title)) return Discard(ReasonPromoTitle);
        if (Roundup().IsMatch(title)) return Discard(ReasonRoundup);
        if (entry.PublishedAt is { } published && now - published > MaxAge) return Discard(ReasonOld);

        var flags = new List<string>();
        if (Clickbait().IsMatch(title) || MostlyUppercase(entry.Title)) flags.Add(FlagClickbait);
        return new TriageResult(true, null, flags);
    }

    private static TriageResult Discard(string reason) => new(false, reason, []);

    private static bool MostlyUppercase(string title)
    {
        var letters = title.Where(char.IsLetter).ToList();
        return letters.Count >= 12 && letters.Count(char.IsUpper) >= letters.Count * 0.7;
    }
}
