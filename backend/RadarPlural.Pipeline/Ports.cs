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

// Resultado no formato do contrato docs/contratos/comparacao-v1.md. Json é a resposta já validada.
// B05 pode trocar Json por um modelo tipado sem mexer no executor.
public sealed record AuditOutcome(string ContractVersion, string Json, bool Simulated);

public interface IAuditor
{
    string Name { get; }
    Task<AuditOutcome> AuditAsync(StorySnapshot story, CancellationToken ct);
}

public interface IAuditStore
{
    Task SaveAsync(Guid storyId, int version, string auditorName, AuditOutcome outcome, CancellationToken ct);
}
