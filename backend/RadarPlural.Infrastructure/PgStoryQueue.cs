using Npgsql;
using RadarPlural.Pipeline;

namespace RadarPlural.Infrastructure;

// Fila de trabalho sobre a própria tabela "Stories" (docs/decisoes/0004). Sem broker externo:
// - FOR UPDATE SKIP LOCKED: duas réplicas nunca pegam a mesma história, e uma não espera a outra;
// - lease (LeaseUntil): se o worker morrer, a história volta para a fila sozinha quando o prazo vence;
// - LeaseOwner + Version no UPDATE final: quem perdeu o lease, ou trabalhou numa versão antiga, não avança o estado.
public sealed class PgStoryQueue(NpgsqlDataSource db) : IStoryQueue
{
    public async Task<IReadOnlyList<ClaimedStory>> ClaimAsync(StageDefinition stage, string workerId, int max, DateTimeOffset now, CancellationToken ct)
    {
        await using var cmd = db.CreateCommand("""
            WITH picked AS (
                SELECT "Id" FROM "Stories"
                WHERE "Status" = $1 AND "SourceCount" >= $2 AND ("LeaseUntil" IS NULL OR "LeaseUntil" <= $3)
                ORDER BY "LastPublishedAt" DESC, "Id"
                LIMIT $4
                FOR UPDATE SKIP LOCKED
            )
            UPDATE "Stories" s
            SET "LeaseOwner" = $5, "LeaseUntil" = $3 + $6, "Attempts" = s."Attempts" + 1
            FROM picked WHERE s."Id" = picked."Id"
            RETURNING s."Id", s."Attempts", s."Version"
            """);
        cmd.Parameters.Add(new() { Value = stage.From });
        cmd.Parameters.Add(new() { Value = stage.MinSources });
        cmd.Parameters.Add(new() { Value = now.ToUniversalTime() });
        cmd.Parameters.Add(new() { Value = max });
        cmd.Parameters.Add(new() { Value = workerId });
        cmd.Parameters.Add(new() { Value = stage.Lease });
        var result = new List<ClaimedStory>();
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct)) result.Add(new ClaimedStory(r.GetGuid(0), r.GetInt32(1), r.GetInt32(2)));
        return result;
    }

    public async Task<CompletionResult> CompleteAsync(StageDefinition stage, ClaimedStory story, string workerId, DateTimeOffset now, CancellationToken ct)
    {
        await using var cmd = db.CreateCommand("""
            UPDATE "Stories" SET "Status" = $4, "LeaseOwner" = NULL, "LeaseUntil" = NULL, "Attempts" = 0, "LastError" = NULL, "UpdatedAt" = $5
            WHERE "Id" = $1 AND "LeaseOwner" = $2 AND "Status" = $3 AND "Version" = $6
            """);
        cmd.Parameters.Add(new() { Value = story.StoryId });
        cmd.Parameters.Add(new() { Value = workerId });
        cmd.Parameters.Add(new() { Value = stage.From });
        cmd.Parameters.Add(new() { Value = stage.To });
        cmd.Parameters.Add(new() { Value = now.ToUniversalTime() });
        cmd.Parameters.Add(new() { Value = story.Version });
        if (await cmd.ExecuteNonQueryAsync(ct) == 1) return CompletionResult.Completed;

        // Não avançou: ou o lease é de outro, ou a história mudou de versão enquanto trabalhávamos.
        // No segundo caso, solta na hora (sem esperar o prazo) para ser refeita com as notícias novas.
        await using var release = db.CreateCommand("""
            UPDATE "Stories" SET "LeaseOwner" = NULL, "LeaseUntil" = NULL, "Attempts" = 0
            WHERE "Id" = $1 AND "LeaseOwner" = $2
            """);
        release.Parameters.Add(new() { Value = story.StoryId });
        release.Parameters.Add(new() { Value = workerId });
        return await release.ExecuteNonQueryAsync(ct) == 1 ? CompletionResult.Stale : CompletionResult.LeaseLost;
    }

    public async Task<FailureResult> FailAsync(StageDefinition stage, ClaimedStory story, string workerId, string error, DateTimeOffset? retryAt, DateTimeOffset now, CancellationToken ct)
    {
        // retryAt nulo = desistiu (falhou). Com retryAt, o LeaseUntil vira "não pegue antes de": é o backoff.
        await using var cmd = db.CreateCommand("""
            UPDATE "Stories" SET
                "Status" = CASE WHEN $3::timestamptz IS NULL THEN 'falhou' ELSE "Status" END,
                "LeaseOwner" = NULL, "LeaseUntil" = $3, "LastError" = $4, "UpdatedAt" = $5
            WHERE "Id" = $1 AND "LeaseOwner" = $2
            """);
        cmd.Parameters.Add(new() { Value = story.StoryId });
        cmd.Parameters.Add(new() { Value = workerId });
        cmd.Parameters.Add(new() { Value = (object?)retryAt?.ToUniversalTime() ?? DBNull.Value, NpgsqlDbType = NpgsqlTypes.NpgsqlDbType.TimestampTz });
        cmd.Parameters.Add(new() { Value = error });
        cmd.Parameters.Add(new() { Value = now.ToUniversalTime() });
        if (await cmd.ExecuteNonQueryAsync(ct) == 0) return FailureResult.LeaseLost;
        return retryAt is null ? FailureResult.GaveUp : FailureResult.Retrying;
    }
}
