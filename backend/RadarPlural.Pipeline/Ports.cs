namespace RadarPlural.Pipeline;

// Portas das próximas etapas (arquitetura hexagonal, aula de 11/11). Os adaptadores ficam fora deste projeto:
// auditor simulado = B05 (Davidy), Gemini = B09, roteirista = #19, HyperFrames = #20.

public sealed record StoryItem(Guid ItemId, string SourceId, string SourceName, string Url, string Title, string Summary, DateTimeOffset? PublishedAt);

public sealed record StorySnapshot(Guid StoryId, int Version, IReadOnlyList<StoryItem> Items);

// Leitura da história com as notícias (texto já higienizado pelo coletor).
public interface IStoryReader
{
    Task<StorySnapshot?> ReadAsync(Guid storyId, CancellationToken ct);
}

// Auditor: compara as notícias de uma história e devolve o contrato v1. Adaptadores: simulado (B05), Gemini (B09).
// A saída de QUALQUER auditor passa por ContractValidator.Sanitize antes de ser gravada ou mostrada.
public interface IAuditor
{
    string Name { get; }
    bool Simulated { get; }
    Task<Comparison.ComparisonResult> AuditAsync(StorySnapshot story, CancellationToken ct);
}

public interface IAuditStore
{
    Task SaveAsync(Comparison.ComparisonResult result, int storyVersion, CancellationToken ct);
    Task<(Comparison.ComparisonResult Result, int StoryVersion)?> LatestAsync(Guid storyId, CancellationToken ct);
}
