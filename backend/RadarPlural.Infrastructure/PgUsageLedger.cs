using Npgsql;
using RadarPlural.Domain.Billing;

namespace RadarPlural.Infrastructure;

// RN03 — reserva de uso atômica no PostgreSQL. Funciona com N réplicas da API (decisoes/0004):
// a condição "usado < quota" (RN01, UsageRules.Decide) é checada e incrementada no MESMO comando,
// então dois pedidos simultâneos nunca passam juntos do limite.
public sealed class PgUsageLedger(NpgsqlDataSource db) : IUsageLedger
{
    public async Task<ReserveResult> ReserveAsync(Guid memberId, Plan plan, Guid key, DateTimeOffset now, CancellationToken ct)
    {
        if (UsageRules.Decide(plan, 0) == UsageDecision.QuotaExceeded) return ReserveResult.QuotaExceeded; // quota zero
        var month = UsageRules.MonthKey(now);
        await using var conn = await db.OpenConnectionAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);

        // 1) A chave registra a intenção. Repetiu a chave (retry do cliente, clique duplo)? Não consome de novo.
        await using (var insert = new NpgsqlCommand("""
            INSERT INTO "UsageReservations" ("Key", "MemberId", "Month", "Status", "CreatedAt")
            VALUES ($1, $2, $3, 'reserved', $4) ON CONFLICT ("Key") DO NOTHING
            """, conn, tx))
        {
            insert.Parameters.Add(new() { Value = key });
            insert.Parameters.Add(new() { Value = memberId });
            insert.Parameters.Add(new() { Value = month });
            insert.Parameters.Add(new() { Value = now.ToUniversalTime() });
            if (await insert.ExecuteNonQueryAsync(ct) == 0)
            {
                await using var owner = new NpgsqlCommand("""SELECT "MemberId" FROM "UsageReservations" WHERE "Key" = $1""", conn, tx);
                owner.Parameters.Add(new() { Value = key });
                if ((Guid)(await owner.ExecuteScalarAsync(ct))! != memberId)
                    throw new InvalidOperationException("Chave de idempotência pertence a outro membro.");
                await tx.CommitAsync(ct);
                return ReserveResult.AlreadyReserved;
            }
        }

        // 2) Incrementa só se ainda houver quota. Sem linha de retorno = quota cheia: desfaz a reserva.
        await using (var bump = new NpgsqlCommand("""
            INSERT INTO "UsageMonths" ("MemberId", "Month", "Used") VALUES ($1, $2, 1)
            ON CONFLICT ("MemberId", "Month") DO UPDATE SET "Used" = "UsageMonths"."Used" + 1
            WHERE "UsageMonths"."Used" < $3
            RETURNING "Used"
            """, conn, tx))
        {
            bump.Parameters.Add(new() { Value = memberId });
            bump.Parameters.Add(new() { Value = month });
            bump.Parameters.Add(new() { Value = plan.MonthlyComparisons });
            if (await bump.ExecuteScalarAsync(ct) is null)
            {
                await tx.RollbackAsync(ct);
                return ReserveResult.QuotaExceeded;
            }
        }
        await tx.CommitAsync(ct);
        return ReserveResult.Reserved;
    }

    // Sucesso: a unidade fica gasta.
    public async Task SettleAsync(Guid key, CancellationToken ct)
    {
        await using var cmd = db.CreateCommand("""UPDATE "UsageReservations" SET "Status" = 'settled' WHERE "Key" = $1 AND "Status" = 'reserved'""");
        cmd.Parameters.Add(new() { Value = key });
        await cmd.ExecuteNonQueryAsync(ct);
    }

    // Falha do provedor: devolve a unidade, uma vez só (só reserva ainda "reserved" volta).
    public async Task ReleaseAsync(Guid key, CancellationToken ct)
    {
        await using var cmd = db.CreateCommand("""
            WITH r AS (
                UPDATE "UsageReservations" SET "Status" = 'released'
                WHERE "Key" = $1 AND "Status" = 'reserved'
                RETURNING "MemberId", "Month"
            )
            UPDATE "UsageMonths" u SET "Used" = u."Used" - 1
            FROM r WHERE u."MemberId" = r."MemberId" AND u."Month" = r."Month" AND u."Used" > 0
            """);
        cmd.Parameters.Add(new() { Value = key });
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<int> UsedAsync(Guid memberId, DateTimeOffset now, CancellationToken ct)
    {
        await using var cmd = db.CreateCommand("""SELECT "Used" FROM "UsageMonths" WHERE "MemberId" = $1 AND "Month" = $2""");
        cmd.Parameters.Add(new() { Value = memberId });
        cmd.Parameters.Add(new() { Value = UsageRules.MonthKey(now) });
        return await cmd.ExecuteScalarAsync(ct) is int used ? used : 0;
    }
}
