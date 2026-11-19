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

    // Áreas do catálogo (o tema de cada fonte). Nome técnico → nome para mostrar na tela.
    public static readonly IReadOnlyDictionary<string, string> Areas = new Dictionary<string, string>
    {
        ["tecnologia"] = "Tecnologia",
        ["ia"] = "Inteligência artificial",
        ["ciencia"] = "Ciência",
        ["espaco"] = "Espaço",
        ["curiosidades"] = "Curiosidades",
        ["brasil-e-mundo"] = "Brasil e mundo",
        ["economia"] = "Economia",
    };

    // Tipo de fonte: contexto para o leitor (issue #23). Não é nota de confiabilidade.
    public static readonly IReadOnlyDictionary<string, string> Kinds = new Dictionary<string, string>
    {
        ["jornalismo"] = "Veículo de jornalismo",
        ["agencia-publica"] = "Agência pública de notícias",
        ["institucional"] = "Órgão público ou instituição",
        ["universidade"] = "Universidade ou fundação de pesquisa",
        ["divulgacao-cientifica"] = "Divulgação científica",
    };

    [GeneratedRegex("^[a-z]{2}$")]
    private static partial Regex LanguagePattern();

    private sealed record Row(string? Id, string? Name, string? Url, string? Topic, List<string>? ExcludeUrlContains, string? Language, string? Kind, string? Reason);

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
            var topic = row.Topic.Trim().ToLowerInvariant();
            if (!Areas.ContainsKey(topic)) throw new IngestionException($"Fonte {label}: área (topic) desconhecida \"{topic}\". Use uma de: {string.Join(", ", Areas.Keys)}.");
            // Curadoria registrada: quem inclui uma fonte diz o idioma, o tipo e por que ela entrou.
            if (string.IsNullOrWhiteSpace(row.Language)) throw new IngestionException($"Fonte {label}: language obrigatório (idioma em 2 letras, ex.: pt).");
            if (!LanguagePattern().IsMatch(row.Language.Trim())) throw new IngestionException($"Fonte {label}: language deve ser o código do idioma em 2 letras minúsculas (ex.: pt, en).");
            if (string.IsNullOrWhiteSpace(row.Kind)) throw new IngestionException($"Fonte {label}: kind obrigatório (tipo da fonte).");
            if (!Kinds.ContainsKey(row.Kind.Trim())) throw new IngestionException($"Fonte {label}: kind \"{row.Kind}\" não é um tipo conhecido. Use um de: {string.Join(", ", Kinds.Keys)}.");
            if (string.IsNullOrWhiteSpace(row.Reason)) throw new IngestionException($"Fonte {label}: reason obrigatório (motivo da escolha).");
            var reason = row.Reason.Trim();
            if (reason.Length is < 20 or > 280) throw new IngestionException($"Fonte {label}: reason (motivo da escolha) precisa ter de 20 a 280 caracteres.");
            var exclude = (row.ExcludeUrlContains ?? []).Select(x => x?.Trim() ?? "").ToList();
            // "/" ou vazio descartaria a fonte inteira por engano.
            if (exclude.Any(x => x.Length < 3 || x.Length > 100)) throw new IngestionException($"Fonte {label}: cada item de excludeUrlContains precisa ter de 3 a 100 caracteres.");
            result.Add(new FeedSource(row.Id, row.Name.Trim(), url, topic, exclude, row.Language.Trim(), row.Kind.Trim(), reason));
        }
        return result;
    }

    public static IReadOnlySet<string> AllowedHosts(IEnumerable<FeedSource> sources) =>
        sources.Select(s => s.Url.IdnHost.ToLowerInvariant()).ToHashSet(StringComparer.OrdinalIgnoreCase);
}
