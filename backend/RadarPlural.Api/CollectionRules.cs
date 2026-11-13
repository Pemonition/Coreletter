using System.Globalization;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

namespace RadarPlural.Api;

public sealed record CollectionInput(string? Topic, string[]? Keywords, string[]? ExcludedKeywords,
    string[]? SourceIds, int IntervalHours, int MaxItems, bool Enabled);
public sealed record FeedSource(string Id, string Name, string Url, string Language, bool Approved = false, string Category = "Geral", string SourceType = "não classificada");
public sealed class CollectionOptions
{
    public bool Enabled { get; set; }
    public FeedSource[] Sources { get; set; } = [];
}
public sealed record FeedEntry(string Url, string Title, string Summary, DateTimeOffset? PublishedAt);

public static class CollectionRules
{
    public static string? Validate(CollectionInput input, IReadOnlyList<FeedSource> sources)
    {
        if (string.IsNullOrWhiteSpace(input.Topic) || input.Topic.Trim().Length > 80)
            return "Informe um tema de até 80 caracteres.";
        if (!ValidTerms(input.Keywords, true) || !ValidTerms(input.ExcludedKeywords, false))
            return "Informe de 1 a 10 palavras ou expressões de busca (até 60 caracteres cada); exclusões são opcionais, até 10.";
        if (sources.Select(s => s.Id).Distinct().Count() != sources.Count)
            return "Catálogo com identificadores duplicados; solicite revisão ao responsável.";
        if (input.SourceIds is null || input.SourceIds.Length is < 1 or > 3 ||
            input.SourceIds.Distinct().Count() != input.SourceIds.Length ||
            input.SourceIds.Any(id => !sources.Any(s => s.Id == id && s.Approved && ValidSourceUrl(s.Url))))
            return "Escolha de 1 a 3 fontes habilitadas no catálogo.";
        if (input.IntervalHours is not (6 or 12 or 24) || input.MaxItems is < 1 or > 20)
            return "Escolha frequência de 6, 12 ou 24 horas e de 1 a 20 resultados.";
        return null;
    }
    static bool ValidTerms(string[]? terms, bool required) => terms is not null &&
        terms.Length <= 10 && (!required || terms.Length > 0) &&
        terms.All(x => !string.IsNullOrWhiteSpace(x) && x.Trim().Length <= 60);

    public static string Normalize(string value)
    {
        var sb = new StringBuilder();
        foreach (var c in value.Normalize(NormalizationForm.FormD))
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                sb.Append(char.ToLowerInvariant(c));
        return sb.ToString().Normalize(NormalizationForm.FormC);
    }
    public static bool Matches(FeedEntry entry, CollectionInput input)
    {
        var text = Normalize(entry.Title + " " + entry.Summary);
        return input.Keywords!.Any(k => text.Contains(Normalize(k.Trim()), StringComparison.Ordinal)) &&
            !input.ExcludedKeywords!.Any(k => text.Contains(Normalize(k.Trim()), StringComparison.Ordinal));
    }
    public static bool ValidSourceUrl(string value) => Uri.TryCreate(value, UriKind.Absolute, out var u) &&
        u.Scheme == "https" && u.Port == 443 && u.HostNameType == UriHostNameType.Dns &&
        u.Host.Contains('.') && !u.Host.EndsWith('.') && string.IsNullOrEmpty(u.UserInfo) && string.IsNullOrEmpty(u.Fragment);

    public static bool PublicIp(IPAddress address)
    {
        if (address.IsIPv4MappedToIPv6) return PublicIp(address.MapToIPv4());
        var b = address.GetAddressBytes();
        if (b.Length == 16)
            return (b[0] & 0xe0) == 0x20 && !(b[0] == 0x20 && b[1] == 0x01 && b[2] < 2)
                && !(b[0] == 0x20 && b[1] == 0x01 && b[2] == 0x0d && b[3] == 0xb8)
                && !(b[0] == 0x20 && b[1] == 0x02); // global unicast only; reject documentation and 6to4
        return b[0] is not (0 or 10 or 127) && b[0] < 224 &&
            !(b[0] == 100 && b[1] is >= 64 and <= 127) && !(b[0] == 169 && b[1] == 254) &&
            !(b[0] == 172 && b[1] is >= 16 and <= 31) && !(b[0] == 192 && b[1] == 168) &&
            !(b[0] == 192 && b[1] == 0) && !(b[0] == 198 && b[1] is 18 or 19 or 51) &&
            !(b[0] == 203 && b[1] == 0 && b[2] == 113);
    }
    public static string Plain(string value, int limit)
    {
        var text = Regex.Replace(WebUtility.HtmlDecode(Regex.Replace(value, "<[^>]*>", " ", RegexOptions.None,
            TimeSpan.FromMilliseconds(100))), @"\s+", " ", RegexOptions.None, TimeSpan.FromMilliseconds(100)).Trim();
        return text[..Math.Min(text.Length, limit)];
    }
    public static string? CanonicalLink(string? value)
    {
        if (value is null || value.Length > 2048 || !ValidSourceUrl(value)) return null;
        var b = new UriBuilder(value) { Fragment = "" };
        return b.Uri.AbsoluteUri;
    }
    public static IReadOnlyList<FeedEntry> Parse(Stream xml)
    {
        using var reader = XmlReader.Create(xml, new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 524288
        });
        var doc = XDocument.Load(reader);
        var root = doc.Root ?? throw new XmlException("Feed vazio.");
        if (root.Name.LocalName is not ("rss" or "feed" or "RDF")) throw new XmlException("Formato RSS/Atom necessário.");
        return root.Descendants().Where(x => x.Name.LocalName is "item" or "entry").Take(100).Select(x =>
        {
            string Text(string name) => x.Elements().FirstOrDefault(e => e.Name.LocalName == name)?.Value ?? "";
            var link = x.Elements().FirstOrDefault(e => e.Name.LocalName == "link" &&
                ((string?)e.Attribute("rel") is null or "alternate"));
            var url = CanonicalLink((string?)link?.Attribute("href") ?? link?.Value);
            var title = Plain(Text("title"), 300);
            var date = Text("pubDate");
            if (date.Length == 0) date = Text("published");
            if (date.Length == 0) date = Text("updated");
            DateTimeOffset? published = DateTimeOffset.TryParse(date, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var d) ? d.ToUniversalTime() : null;
            return url is null || title.Length == 0 ? null : new FeedEntry(url, title,
                Plain(Text("description") is { Length: > 0 } description ? description : Text("summary"), 600), published);
        }).OfType<FeedEntry>().DistinctBy(x => x.Url).ToArray();
    }
}
