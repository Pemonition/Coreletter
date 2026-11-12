using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

namespace RadarPlural.Ingestion;

// Lê RSS 2.0/0.9x, RSS 1.0 (RDF) e Atom. Todo texto vindo do feed é dado não confiável:
// sai daqui como texto puro, truncado, sem HTML, e nunca é interpretado como instrução.
public static partial class FeedParser
{
    private static readonly XNamespace Atom = "http://www.w3.org/2005/Atom";
    private static readonly XNamespace Rss1 = "http://purl.org/rss/1.0/";
    private static readonly XNamespace Dc = "http://purl.org/dc/elements/1.1/";

    public static IReadOnlyList<FeedEntry> Parse(Stream xml, Uri feedUrl, IngestionPolicy policy)
    {
        var settings = new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit, // bloqueia XXE e "billion laughs"
            XmlResolver = null,
            IgnoreComments = true,
            IgnoreProcessingInstructions = true,
            MaxCharactersInDocument = policy.MaxBytes * 2
        };
        XElement root;
        try
        {
            using var reader = XmlReader.Create(xml, settings);
            root = XDocument.Load(reader).Root ?? throw new IngestionException("Feed vazio.");
        }
        catch (XmlException e)
        {
            throw new IngestionException($"XML inválido: {e.Message}", e);
        }

        IEnumerable<RawEntry> raw = root.Name switch
        {
            { LocalName: "rss" } => (root.Element("channel")?.Elements("item") ?? []).Select(ReadRss),
            { LocalName: "feed" } when root.Name.Namespace == Atom => root.Elements(Atom + "entry").Select(ReadAtom),
            { LocalName: "RDF" } => root.Elements(Rss1 + "item").Select(ReadRdf),
            _ => throw new IngestionException($"Formato não reconhecido: <{root.Name.LocalName}>.")
        };

        var seen = new HashSet<string>(StringComparer.Ordinal);
        var result = new List<FeedEntry>();
        foreach (var entry in raw)
        {
            var title = Clean(entry.Title, policy.MaxTitleLength);
            var url = UrlNormalizer.Normalize(entry.Link, feedUrl);
            if (title.Length == 0 || url is null || !seen.Add(url)) continue;
            result.Add(new FeedEntry(title, url, Clean(entry.Summary, policy.MaxSummaryLength), ParseDate(entry.Date)));
            if (result.Count >= policy.MaxEntriesPerFeed) break;
        }
        return result;
    }

    private sealed record RawEntry(string? Title, string? Link, string? Summary, string? Date);

    private static RawEntry ReadRss(XElement item)
    {
        var link = item.Element("link")?.Value;
        var guid = item.Element("guid");
        if (string.IsNullOrWhiteSpace(link) && guid is not null && !string.Equals((string?)guid.Attribute("isPermaLink"), "false", StringComparison.OrdinalIgnoreCase))
            link = guid.Value;
        return new(item.Element("title")?.Value, link, item.Element("description")?.Value,
            item.Element("pubDate")?.Value ?? item.Element(Dc + "date")?.Value);
    }

    private static RawEntry ReadAtom(XElement entry)
    {
        var link = entry.Elements(Atom + "link")
            .FirstOrDefault(l => (string?)l.Attribute("rel") is null or "alternate")?.Attribute("href")?.Value;
        return new(entry.Element(Atom + "title")?.Value, link,
            entry.Element(Atom + "summary")?.Value ?? entry.Element(Atom + "content")?.Value,
            entry.Element(Atom + "published")?.Value ?? entry.Element(Atom + "updated")?.Value);
    }

    private static RawEntry ReadRdf(XElement item) =>
        new(item.Element(Rss1 + "title")?.Value, item.Element(Rss1 + "link")?.Value,
            item.Element(Rss1 + "description")?.Value, item.Element(Dc + "date")?.Value);

    // ---------- texto ----------

    [GeneratedRegex(@"<(script|style)\b[^>]*>.*?</\1\s*>", RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex ScriptOrStyle();

    [GeneratedRegex("<[^>]*>")]
    private static partial Regex Tag();

    [GeneratedRegex(@"[\p{Cc}\p{Cf}\s]+")]
    private static partial Regex WhitespaceOrControl();

    // Tags inline removidas deixam "Chip , lançado ( hoje )"; junta de volta à pontuação.
    [GeneratedRegex(@"\s+(?=[.,;:!?)\]])|(?<=[(\[])\s+")]
    private static partial Regex SpaceAroundPunctuation();

    public static string Clean(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value)) return "";
        var text = ScriptOrStyle().Replace(value, " ");
        text = Tag().Replace(text, " ");
        text = WebUtility.HtmlDecode(text);
        text = WhitespaceOrControl().Replace(text, " ").Trim();
        text = SpaceAroundPunctuation().Replace(text, "");
        return Truncate(text, maxLength);
    }

    private static string Truncate(string text, int max)
    {
        if (text.Length <= max) return text;
        var cut = text[..(max - 1)];
        var space = cut.LastIndexOf(' ');
        if (space > max / 2) cut = cut[..space];
        return cut.TrimEnd() + "…";
    }

    // ---------- datas ----------

    [GeneratedRegex(@"^(?:[A-Za-z]{3},?\s*)?(\d{1,2})\s+([A-Za-z]{3})[a-z]*\s+(\d{2,4})\s+(\d{1,2}):(\d{2})(?::(\d{2}))?\s*(.*)$")]
    private static partial Regex Rfc822();

    private static readonly string[] Months = ["jan", "feb", "mar", "apr", "may", "jun", "jul", "aug", "sep", "oct", "nov", "dec"];

    private static readonly Dictionary<string, int> NamedZones = new(StringComparer.OrdinalIgnoreCase)
    {
        ["GMT"] = 0, ["UT"] = 0, ["UTC"] = 0, ["Z"] = 0,
        ["EST"] = -5, ["EDT"] = -4, ["CST"] = -6, ["CDT"] = -5, ["MST"] = -7, ["MDT"] = -6, ["PST"] = -8, ["PDT"] = -7,
        ["BRT"] = -3, ["BRST"] = -2
    };

    public static DateTimeOffset? ParseDate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        value = value.Trim();
        var m = Rfc822().Match(value);
        if (m.Success)
        {
            var month = Array.IndexOf(Months, m.Groups[2].Value.ToLowerInvariant()) + 1;
            var year = int.Parse(m.Groups[3].Value, CultureInfo.InvariantCulture);
            if (year < 100) year += 2000;
            var offset = ParseZone(m.Groups[7].Value.Trim());
            if (month == 0 || offset is null) return null;
            try
            {
                return new DateTimeOffset(year, month, int.Parse(m.Groups[1].Value), int.Parse(m.Groups[4].Value), int.Parse(m.Groups[5].Value),
                    m.Groups[6].Success ? int.Parse(m.Groups[6].Value) : 0, offset.Value);
            }
            catch (ArgumentOutOfRangeException)
            {
                return null;
            }
        }
        // ISO 8601 (Atom, dc:date). Só aceita se começar como data, para não "adivinhar" textos livres.
        return char.IsDigit(value[0]) && DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var iso) ? iso : null;
    }

    private static TimeSpan? ParseZone(string zone)
    {
        if (zone.Length == 0) return TimeSpan.Zero;
        if (NamedZones.TryGetValue(zone, out var hours)) return TimeSpan.FromHours(hours);
        var digits = zone.Replace(":", "");
        if (digits.Length == 5 && digits[0] is '+' or '-' && int.TryParse(digits.AsSpan(1, 2), out var h) && int.TryParse(digits.AsSpan(3, 2), out var min) && h <= 14 && min < 60)
        {
            var span = new TimeSpan(h, min, 0);
            return digits[0] == '-' ? -span : span;
        }
        return null;
    }
}
