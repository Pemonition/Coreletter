namespace RadarPlural.Ingestion;

// Fonte aprovada na política de ingestão (docs/decisoes/0002-ingestao.md). Só entra por PR revisada.
public sealed record FeedSource(string Id, string Name, Uri Url, string Topic);

// Item já higienizado: texto puro, URL absoluta normalizada, tamanhos dentro da política.
public sealed record FeedEntry(string Title, string Url, string Summary, DateTimeOffset? PublishedAt);

public sealed record IngestionPolicy
{
    public long MaxBytes { get; init; } = 2_000_000;
    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(15);
    public int MaxRedirects { get; init; } = 3;
    public int MaxEntriesPerFeed { get; init; } = 50;
    public int MaxTitleLength { get; init; } = 300;
    // Trecho curto para o card (direito autoral): a matéria completa é lida no site de origem, pelo link.
    public int MaxSummaryLength { get; init; } = 300;
    // Formato padrão de robô que se identifica (como o Googlebot). Nunca se passar por navegador para driblar bloqueio.
    public string UserAgent { get; init; } = "Mozilla/5.0 (compatible; CoreletterBot/0.1; +https://github.com/Pemonition/Coreletter)";
}

// Falha esperada de uma fonte (bloqueio de política, limite, formato). Não derruba as demais fontes.
public sealed class IngestionException(string message, Exception? inner = null) : Exception(message, inner);
