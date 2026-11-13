using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace RadarPlural.Ingestion;

// Lista de fontes aprovadas (fontes.json). É a lista de permissão do coletor: só esses hosts são acessados.
// Mudança na lista = PR revisada, conforme docs/decisoes/0002-ingestao.md.
public static partial class SourceCatalog
{
    [GeneratedRegex("^[a-z0-9][a-z0-9-]{0,63}$")]
    private static partial Regex IdPattern();

    private sealed record Row(string? Id, string? Name, string? Url, string? Topic, List<string>? ExcludeUrlContains);

    public static IReadOnlyList<FeedSource> Parse(string json)
    {
        List<Row>? rows;
        try
        {
            rows = JsonSerializer.Deserialize<List<Row>>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true, ReadCommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
        }
        catch (JsonException e)
        {
            throw new IngestionException($"fontes.json inválido: {e.Message}", e);
        }
        if (rows is null || rows.Count == 0) throw new IngestionException("fontes.json não tem nenhuma fonte.");

        var ids = new HashSet<string>(StringComparer.Ordinal);
        var result = new List<FeedSource>();
        foreach (var row in rows)
        {
            var label = row.Id ?? "(sem id)";
            if (row.Id is null || !IdPattern().IsMatch(row.Id)) throw new IngestionException($"Fonte {label}: id deve ter só letras minúsculas, números e hífen.");
            if (!ids.Add(row.Id)) throw new IngestionException($"Fonte {label}: id repetido.");
            if (string.IsNullOrWhiteSpace(row.Name) || row.Name.Length > 120) throw new IngestionException($"Fonte {label}: nome obrigatório (até 120).");
            if (string.IsNullOrWhiteSpace(row.Topic) || row.Topic.Length > 60) throw new IngestionException($"Fonte {label}: tema obrigatório (até 60).");
            if (!Uri.TryCreate(row.Url, UriKind.Absolute, out var url) || url.Scheme != Uri.UriSchemeHttps || !url.IsDefaultPort
                || !string.IsNullOrEmpty(url.UserInfo) || url.HostNameType != UriHostNameType.Dns || IPAddress.TryParse(url.Host, out _))
                throw new IngestionException($"Fonte {label}: URL precisa ser https, com nome de domínio, porta padrão e sem credenciais.");
            var exclude = (row.ExcludeUrlContains ?? []).Select(x => x?.Trim() ?? "").ToList();
            // "/" ou vazio descartaria a fonte inteira por engano.
            if (exclude.Any(x => x.Length < 3 || x.Length > 100)) throw new IngestionException($"Fonte {label}: cada item de excludeUrlContains precisa ter de 3 a 100 caracteres.");
            result.Add(new FeedSource(row.Id, row.Name.Trim(), url, row.Topic.Trim().ToLowerInvariant(), exclude));
        }
        return result;
    }

    public static IReadOnlySet<string> AllowedHosts(IEnumerable<FeedSource> sources) =>
        sources.Select(s => s.Url.IdnHost.ToLowerInvariant()).ToHashSet(StringComparer.OrdinalIgnoreCase);
}
