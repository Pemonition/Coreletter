namespace RadarPlural.Ingestion;

public static class FeedDates
{
    // Data no futuro (fuso errado ou feed mal configurado) fixaria a notícia no topo do feed para sempre.
    // Tolera 1 hora de diferença de relógio; acima disso a data é descartada e vale a hora da coleta.
    public static DateTimeOffset? PublishedOrNull(DateTimeOffset? published, DateTimeOffset collectedAt) =>
        published is { } p && p <= collectedAt.AddHours(1) ? p : null;
}
