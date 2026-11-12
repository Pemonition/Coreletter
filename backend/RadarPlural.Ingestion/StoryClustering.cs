using System.Globalization;
using System.Text;

namespace RadarPlural.Ingestion;

public sealed record ClusterCandidate(Guid Id, string SourceId, string Title, DateTimeOffset At);

public sealed record StoryAssignment(Guid StoryId, IReadOnlyList<Guid> ItemIds, bool IsNew);

// Palavras relevantes de uma manchete: minúsculas, sem acento, sem palavras vazias, sem números soltos.
public static class HeadlineTokens
{
    // Palavras vazias do português + jargão de manchete + dias e meses (não identificam uma história).
    private static readonly HashSet<string> Stopwords =
    [
        "a", "o", "as", "os", "um", "uma", "uns", "umas", "de", "do", "da", "dos", "das", "em", "no", "na", "nos", "nas",
        "ao", "aos", "por", "pelo", "pela", "pelos", "pelas", "para", "pra", "com", "sem", "sob", "sobre", "entre", "ate",
        "apos", "antes", "depois", "desde", "contra", "e", "ou", "mas", "que", "se", "como", "quando", "onde", "porque",
        "qual", "quais", "quem", "seu", "sua", "seus", "suas", "isso", "isto", "esse", "essa", "este", "esta", "nesta",
        "neste", "nessa", "nesse", "mais", "menos", "muito", "ja", "nao", "sim", "tem", "ter", "ser", "esta", "sao", "foi",
        "vai", "vao", "pode", "podem", "deve", "devem", "todo", "toda", "todos", "todas", "outro", "outra", "agora",
        "veja", "confira", "entenda", "saiba", "novo", "nova", "novos", "novas", "ganha", "chega", "diz", "afirma",
        "segundo", "hoje", "ontem", "amanha", "semana", "ano", "anos", "mes", "meses", "dia", "dias",
        "segunda", "terca", "quarta", "quinta", "sexta", "sabado", "domingo", "feira",
        // Cargos e termos corporativos genéricos: ligavam histórias sem relação ("CEO da Verizon" ~ "ex-CEO da Nvidia").
        "ceo", "presidente", "diretor", "executivo", "empresa", "empresas", "prepara", "anuncia", "lanca", "confirma",
        "janeiro", "fevereiro", "marco", "abril", "maio", "junho", "julho", "agosto", "setembro", "outubro", "novembro", "dezembro"
    ];

    public static IReadOnlySet<string> Of(string title) => Words(title).Select(w => w.Token).Where(Keep).ToHashSet();

    // Nomes próprios: palavra com maiúscula que não começa frase (Nvidia, Huang, FUST, iPhone...).
    // Depois de "?", "!", ":" ou "." a maiúscula é de início de frase ("Gov.br caiu? Serviços..."), não nome.
    public static IReadOnlySet<string> Names(string title) =>
        Words(title).Where(w => !w.SentenceStart && w.Original.Any(char.IsUpper)).Select(w => w.Token).Where(Keep).ToHashSet();

    private static bool Keep(string t) => !Stopwords.Contains(t) && !t.All(char.IsDigit) && (t.Length >= 3 || t.Any(char.IsDigit));

    private static IEnumerable<(string Original, string Token, bool SentenceStart)> Words(string title)
    {
        var sentenceStart = true;
        foreach (var raw in title.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
        {
            foreach (var part in SplitNonAlnum(raw))
            {
                yield return (part, Singular(Fold(part)), sentenceStart);
                sentenceStart = false;
            }
            if (raw.TrimEnd('"', '\'', '’', '”', ')').EndsWith('?') || raw.EndsWith('!') || raw.EndsWith(':') || raw.EndsWith('.')) sentenceStart = true;
        }
    }

    // Plural simples ("pré-vendas" ~ "pré-venda", "aplicativos" ~ "aplicativo"). Palavras curtas ficam como estão.
    private static string Singular(string t) => t.Length > 4 && t.EndsWith('s') && !t.EndsWith("ss") ? t[..^1] : t;

    private static IEnumerable<string> SplitNonAlnum(string s)
    {
        var sb = new StringBuilder();
        foreach (var c in s)
        {
            if (char.IsLetterOrDigit(c)) sb.Append(c);
            else if (sb.Length > 0) { yield return sb.ToString(); sb.Clear(); }
        }
        if (sb.Length > 0) yield return sb.ToString();
    }

    public static string Fold(string s)
    {
        var d = s.ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(d.Length);
        foreach (var c in d)
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark) sb.Append(c);
        return sb.ToString().Normalize(NormalizationForm.FormC);
    }
}

// Agrupa notícias sobre o mesmo acontecimento. Regra simples e explicável (calibrada com ~400 manchetes reais de 12/11/2026):
// duas manchetes publicadas a até 48 h uma da outra são da mesma história quando compartilham
// 3+ palavras relevantes, sendo ao menos 1 nome próprio.
// "2 nomes em comum" foi testado e descartado: juntava eventos diferentes do mesmo assunto ("GTA 6" + "Xbox").
// Ligação transitiva (A~B e B~C ⇒ A, B, C juntos), o que junta manchetes bem diferentes via uma terceira.
public static class StoryClusterer
{
    public const int MinSharedWords = 3;

    public static IReadOnlyList<IReadOnlyList<Guid>> Cluster(IReadOnlyList<ClusterCandidate> items, TimeSpan? window = null)
    {
        var maxGap = window ?? TimeSpan.FromHours(48);
        var ordered = items.OrderBy(x => x.Id).ToArray(); // ordem fixa: resultado não depende da ordem de entrada
        var words = ordered.Select(x => HeadlineTokens.Of(x.Title)).ToArray();
        var names = ordered.Select(x => HeadlineTokens.Names(x.Title)).ToArray();
        var parent = Enumerable.Range(0, ordered.Length).ToArray();

        int Find(int i) => parent[i] == i ? i : parent[i] = Find(parent[i]);

        for (var i = 0; i < ordered.Length; i++)
            for (var j = i + 1; j < ordered.Length; j++)
            {
                if ((ordered[i].At - ordered[j].At).Duration() > maxGap) continue;
                if (SameStory(words[i], names[i], words[j], names[j])) parent[Find(i)] = Find(j);
            }

        return Enumerable.Range(0, ordered.Length)
            .GroupBy(Find)
            .Select(g => (IReadOnlyList<Guid>)g.Select(i => ordered[i].Id).ToList())
            .ToList();
    }

    public static bool SameStory(IReadOnlySet<string> wordsA, IReadOnlySet<string> namesA, IReadOnlySet<string> wordsB, IReadOnlySet<string> namesB)
    {
        var shared = wordsA.Intersect(wordsB).ToList();
        return shared.Count >= MinSharedWords && shared.Any(t => namesA.Contains(t) || namesB.Contains(t));
    }
}

// Mantém o id da história estável entre execuções: auditoria, roteiro e vídeo já feitos ficam ligados à mesma história.
public static class StoryReconciler
{
    public static IReadOnlyList<StoryAssignment> Reconcile(IReadOnlyList<IReadOnlyList<Guid>> clusters, IReadOnlyDictionary<Guid, Guid> existingStoryOfItem, Func<Guid> newId)
    {
        var used = new HashSet<Guid>();
        var result = new List<StoryAssignment>();
        // Grupos maiores escolhem primeiro: numa divisão, a parte maior herda o id.
        foreach (var cluster in clusters.OrderByDescending(c => c.Count).ThenBy(c => c.Min()))
        {
            var candidate = cluster
                .Where(existingStoryOfItem.ContainsKey)
                .GroupBy(i => existingStoryOfItem[i])
                .OrderByDescending(g => g.Count()).ThenBy(g => g.Key)
                .Select(g => g.Key)
                .FirstOrDefault(id => !used.Contains(id));
            var isNew = candidate == Guid.Empty;
            var storyId = isNew ? newId() : candidate;
            used.Add(storyId);
            result.Add(new StoryAssignment(storyId, cluster.Order().ToList(), isNew));
        }
        return result;
    }
}
