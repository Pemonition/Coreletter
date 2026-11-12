namespace RadarPlural.Pipeline;

// Estados de uma história no pipeline (docs/decisoes/0003 e 0004). Cada etapa leva de um estado ao próximo.
public static class StoryStatus
{
    public const string Agrupada = "agrupada";
    public const string Auditada = "auditada";
    public const string Roteirizada = "roteirizada";
    public const string VideoPronto = "video_pronto";
    public const string Falhou = "falhou";
}

// Uma etapa: de qual estado pega, para qual leva, quantas fontes a história precisa ter,
// quantas tentativas antes de desistir e por quanto tempo um worker "segura" a história (lease).
public sealed record StageDefinition(string Name, string From, string To, int MinSources, int MaxAttempts, TimeSpan Lease);

public static class Stages
{
    // Só histórias corroboradas (2+ fontes) seguem: limita custo de IA e de vídeo (regra de rentabilidade, 0003 §5).
    public static readonly StageDefinition Auditoria = new("auditoria", StoryStatus.Agrupada, StoryStatus.Auditada, 2, 3, TimeSpan.FromMinutes(5));
    public static readonly StageDefinition Roteiro = new("roteiro", StoryStatus.Auditada, StoryStatus.Roteirizada, 2, 3, TimeSpan.FromMinutes(5));
    public static readonly StageDefinition Video = new("video", StoryStatus.Roteirizada, StoryStatus.VideoPronto, 2, 2, TimeSpan.FromMinutes(20));

    public static readonly IReadOnlyList<StageDefinition> All = [Auditoria, Roteiro, Video];

    public static StageDefinition? Find(string name) => All.FirstOrDefault(s => s.Name == name);
}

// História reservada por um worker. Attempt já conta esta tentativa; Version detecta história alterada no meio.
public sealed record ClaimedStory(Guid StoryId, int Attempt, int Version);

public enum CompletionResult { Completed, Stale, LeaseLost }

public enum FailureResult { Retrying, GaveUp, LeaseLost }

// Porta de saída: a fila de trabalho (implementada sobre o PostgreSQL em RadarPlural.Infrastructure).
public interface IStoryQueue
{
    Task<IReadOnlyList<ClaimedStory>> ClaimAsync(StageDefinition stage, string workerId, int max, DateTimeOffset now, CancellationToken ct);
    Task<CompletionResult> CompleteAsync(StageDefinition stage, ClaimedStory story, string workerId, DateTimeOffset now, CancellationToken ct);
    Task<FailureResult> FailAsync(StageDefinition stage, ClaimedStory story, string workerId, string error, DateTimeOffset? retryAt, DateTimeOffset now, CancellationToken ct);
}

public abstract record StageOutcome
{
    public sealed record Done : StageOutcome;
    public sealed record Retry(string Error) : StageOutcome;
}

// Porta de entrada de cada etapa: o trabalho em si (auditar, roteirizar, renderizar).
public interface IStageHandler
{
    StageDefinition Stage { get; }
    Task<StageOutcome> HandleAsync(ClaimedStory story, CancellationToken ct);
}
