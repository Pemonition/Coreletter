using RadarPlural.Pipeline.Comparison;

namespace RadarPlural.Pipeline;

// Caso de uso da etapa de auditoria: lê a história, chama o auditor (porta), valida pelo contrato e grava.
public sealed class AuditStageHandler(IStoryReader reader, IAuditor auditor, IAuditStore store) : IStageHandler
{
    public StageDefinition Stage => Stages.Auditoria;

    public async Task<StageOutcome> HandleAsync(ClaimedStory story, CancellationToken ct)
    {
        var snapshot = await reader.ReadAsync(story.StoryId, ct);
        if (snapshot is null || snapshot.Items.Count < 2) return new StageOutcome.Retry("História sem notícias suficientes para comparar.");
        var raw = await auditor.AuditAsync(snapshot, ct);
        var clean = ContractValidator.Sanitize(raw with { StoryId = story.StoryId, Provider = auditor.Name, Simulated = auditor.Simulated });
        // Grava com a versão reservada: se o agrupamento mudar a história, o resultado é reconhecido como antigo.
        await store.SaveAsync(clean, story.Version, ct);
        return new StageOutcome.Done();
    }
}
