namespace RadarPlural.Domain.News;

// Notícia coletada de fonte aprovada. Guarda só título, resumo curto e link: o texto completo fica no site de origem.
public class FeedItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string SourceId { get; set; } = "";
    public string SourceName { get; set; } = "";
    public string Topic { get; set; } = "";
    public string Url { get; set; } = "";
    public string Title { get; set; } = "";
    public string Summary { get; set; } = "";
    public DateTimeOffset? PublishedAt { get; set; }
    public DateTimeOffset CollectedAt { get; set; } = DateTimeOffset.UtcNow;
}
