namespace RadarPlural.Ingestion;

// URL canônica usada como chave de deduplicação: a mesma matéria vinda com parâmetros de rastreio
// diferentes (utm_*, fbclid...) vira uma linha só no banco.
public static class UrlNormalizer
{
    public const int MaxLength = 2048;

    private static readonly HashSet<string> TrackingKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "fbclid", "gclid", "dclid", "msclkid", "igshid", "mc_cid", "mc_eid", "_ga", "ref_src"
    };

    public static string? Normalize(string? raw, Uri? baseUri = null)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        raw = raw.Trim();
        Uri? uri;
        if (!Uri.TryCreate(raw, UriKind.Absolute, out uri) && (baseUri is null || !Uri.TryCreate(baseUri, raw, out uri))) return null;
        // Em Linux, "/x" é lido como file:///x; com base conhecida, trata como caminho relativo do site.
        if (uri.IsFile && baseUri is not null && raw.StartsWith('/') && !Uri.TryCreate(baseUri, raw, out uri)) return null;
        if (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp) return null;
        if (!string.IsNullOrEmpty(uri.UserInfo) || string.IsNullOrWhiteSpace(uri.Host)) return null;

        var query = string.Join('&', uri.Query.TrimStart('?')
            .Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Where(pair =>
            {
                var key = Uri.UnescapeDataString(pair.Split('=', 2)[0]);
                return !key.StartsWith("utm_", StringComparison.OrdinalIgnoreCase) && !TrackingKeys.Contains(key);
            }));

        var builder = new UriBuilder(uri)
        {
            Scheme = uri.Scheme.ToLowerInvariant(),
            Host = uri.IdnHost.ToLowerInvariant(),
            Port = uri.IsDefaultPort ? -1 : uri.Port,
            Query = query,
            Fragment = ""
        };
        var result = builder.Uri.AbsoluteUri;
        return result.Length <= MaxLength ? result : null;
    }
}
