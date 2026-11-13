using RadarPlural.Domain.Billing;

namespace RadarPlural.Pipeline.Comparison;

public sealed record CompareOutcome(ComparisonResult? Result, string? ErrorCode, int Units, bool FromCache);

// Caso de uso "comparar uma história agora" (POST /api/comparisons, contrato v1 + B07).
public sealed class CompareStory(IStoryReader reader, IAuditor auditor, IAuditStore store, IUsageLedger ledger, TimeProvider clock)
{
    public static readonly TimeSpan CacheFor = TimeSpan.FromHours(24);

    public async Task<CompareOutcome> RunAsync(Guid memberId, Plan plan, Guid storyId, Guid idempotencyKey, CancellationToken ct)
    {
        var story = await reader.ReadAsync(storyId, ct);
        if (story is null) return new(null, "historia_inexistente", 0, false);
        if (story.Items.Count < 2) return new(null, "entrada_invalida", 0, false);

        // Mesma versão da história comparada nas últimas 24 h: devolve sem gastar quota (contrato v1, "Cache").
        var latest = await store.LatestAsync(storyId, ct);
        if (latest is { } cached && cached.StoryVersion == story.Version && clock.GetUtcNow() - cached.Result.CreatedAt < CacheFor)
            return new(cached.Result, null, 0, true);

        // RN03: reserva ANTES de chamar o provedor.
        var reservation = await ledger.ReserveAsync(memberId, plan, idempotencyKey, clock.GetUtcNow(), ct);
        if (reservation == ReserveResult.QuotaExceeded) return new(null, "quota_esgotada", 0, false);
        var charged = reservation == ReserveResult.Reserved;
        try
        {
            var raw = await auditor.AuditAsync(story, ct);
            var clean = ContractValidator.Sanitize(raw with { StoryId = storyId, Provider = auditor.Name, Simulated = auditor.Simulated });
            await store.SaveAsync(clean, story.Version, ct);
            if (charged) await ledger.SettleAsync(idempotencyKey, ct);
            return new(clean, null, charged ? 1 : 0, false);
        }
        catch (Exception) when (!ct.IsCancellationRequested)
        {
            // Provedor falhou: devolve a unidade (o usuário não paga pelo erro).
            if (charged) await ledger.ReleaseAsync(idempotencyKey, CancellationToken.None);
            return new(null, "provedor_indisponivel", 0, false);
        }
    }
}
