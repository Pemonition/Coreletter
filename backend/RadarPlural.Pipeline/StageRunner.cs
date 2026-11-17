namespace RadarPlural.Pipeline;

// Nova tentativa espera cada vez mais (1, 2, 4, 8 min… até 1 h): provedor fora do ar não vira avalanche de chamadas.
public static class RetryPolicy
{
    private static readonly TimeSpan Max = TimeSpan.FromHours(1);

    public static TimeSpan Backoff(int attempt)
    {
        if (attempt < 1) attempt = 1;
        var minutes = Math.Pow(2, Math.Min(attempt - 1, 16));
        var delay = TimeSpan.FromMinutes(minutes);
        return delay > Max ? Max : delay;
    }

    public static bool Exhausted(int attempt, int maxAttempts) => attempt >= maxAttempts;
}

public sealed record RunSummary(int Claimed, int Completed, int Retrying, int GaveUp, int Stale, int LeaseLost);

// Executa um lote de uma etapa: reserva histórias, processa uma a uma e registra o resultado.
// Sem estado próprio: N réplicas podem rodar ao mesmo tempo; quem garante "uma história, um worker" é a fila.
public sealed class StageRunner(IStoryQueue queue, IStageHandler handler, string workerId, TimeProvider clock)
{
    public const int MaxBatch = 100;
    public const int MaxErrorLength = 500;

    public async Task<RunSummary> RunOnceAsync(int batchSize, CancellationToken ct)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(batchSize, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(batchSize, MaxBatch);
        ct.ThrowIfCancellationRequested();

        var stage = handler.Stage;
        var claimed = await queue.ClaimAsync(stage, workerId, batchSize, clock.GetUtcNow(), ct);
        int completed = 0, retrying = 0, gaveUp = 0, stale = 0, lost = 0;

        foreach (var story in claimed)
        {
            // Cancelamento (SIGTERM do Kubernetes): para antes da próxima; o lease expira e outra réplica retoma.
            ct.ThrowIfCancellationRequested();
            StageOutcome outcome;
            try
            {
                outcome = await handler.HandleAsync(story, ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception e)
            {
                outcome = new StageOutcome.Retry($"{e.GetType().Name}: {e.Message}");
            }

            var now = clock.GetUtcNow();
            switch (outcome)
            {
                case StageOutcome.Done:
                    switch (await queue.CompleteAsync(stage, story, workerId, now, ct))
                    {
                        case CompletionResult.Completed: completed++; break;
                        case CompletionResult.Stale: stale++; break;
                        case CompletionResult.LeaseLost: lost++; break;
                    }
                    break;
                case StageOutcome.Retry retry:
                    var error = retry.Error.Length > MaxErrorLength ? retry.Error[..MaxErrorLength] : retry.Error;
                    DateTimeOffset? retryAt = RetryPolicy.Exhausted(story.Attempt, stage.MaxAttempts) ? null : now + RetryPolicy.Backoff(story.Attempt);
                    switch (await queue.FailAsync(stage, story, workerId, error, retryAt, now, ct))
                    {
                        case FailureResult.Retrying: retrying++; break;
                        case FailureResult.GaveUp: gaveUp++; break;
                        case FailureResult.LeaseLost: lost++; break;
                    }
                    break;
            }
        }
        return new RunSummary(claimed.Count, completed, retrying, gaveUp, stale, lost);
    }
}
