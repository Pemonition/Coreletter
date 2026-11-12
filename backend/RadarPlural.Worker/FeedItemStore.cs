using Npgsql;
using RadarPlural.Ingestion;

namespace RadarPlural.Worker;

// Grava no esquema criado pelas migrações da API (tabelas "FeedItems" e "Stories"); o Worker não altera o esquema.
// ON CONFLICT ("Url") DO NOTHING: rodar o coletor de novo nunca duplica notícia.
public sealed class FeedItemStore(NpgsqlDataSource db)
{
    public async Task<int> SaveAsync(FeedSource source, IReadOnlyList<(FeedEntry Entry, TriageResult Triage)> entries, DateTimeOffset collectedAt, CancellationToken ct)
    {
        if (entries.Count == 0) return 0;
        await using var batch = db.CreateBatch();
        foreach (var (e, t) in entries)
        {
            var cmd = new NpgsqlBatchCommand("""
                INSERT INTO "FeedItems" ("Id", "SourceId", "SourceName", "Topic", "Url", "Title", "Summary", "PublishedAt", "CollectedAt", "Status", "DiscardReason", "Flags")
                VALUES ($1, $2, $3, $4, $5, $6, $7, $8, $9, $10, $11, $12)
                ON CONFLICT ("Url") DO NOTHING
                """);
            cmd.Parameters.Add(new() { Value = Guid.NewGuid() });
            cmd.Parameters.Add(new() { Value = source.Id });
            cmd.Parameters.Add(new() { Value = source.Name });
            cmd.Parameters.Add(new() { Value = source.Topic });
            cmd.Parameters.Add(new() { Value = e.Url });
            cmd.Parameters.Add(new() { Value = e.Title });
            cmd.Parameters.Add(new() { Value = e.Summary });
            cmd.Parameters.Add(new() { Value = (object?)FeedDates.PublishedOrNull(e.PublishedAt, collectedAt)?.ToUniversalTime() ?? DBNull.Value });
            cmd.Parameters.Add(new() { Value = collectedAt.ToUniversalTime() });
            cmd.Parameters.Add(new() { Value = t.Accepted ? "triado" : "descartado" });
            cmd.Parameters.Add(new() { Value = (object?)t.DiscardReason ?? DBNull.Value });
            cmd.Parameters.Add(new() { Value = string.Join(',', t.Flags) });
            batch.BatchCommands.Add(cmd);
        }
        return await batch.ExecuteNonQueryAsync(ct);
    }
}

// Agrupa as notícias triadas recentes em histórias e mantém os ids estáveis (StoryReconciler).
// Tudo numa transação: quem lê a API nunca vê uma história pela metade.
public sealed class StoryStore(NpgsqlDataSource db)
{
    // Carrega 72 h para que notícias perto da borda da janela de 48 h ainda encontrem par.
    private static readonly TimeSpan LoadWindow = TimeSpan.FromHours(72);

    // Regras de triagem mudam com o tempo: reavalia as notícias recentes que ainda estão "triado".
    // Só rebaixa (triado → descartado); nunca ressuscita o que já foi descartado.
    public async Task<int> RetriageAsync(IReadOnlyDictionary<string, FeedSource> sources, DateTimeOffset now, CancellationToken ct)
    {
        var toDiscard = new List<(Guid Id, string Reason)>();
        await using (var cmd = db.CreateCommand("""
            SELECT "Id", "SourceId", "Title", "Url", "Summary", "PublishedAt" FROM "FeedItems"
            WHERE "Status" = 'triado' AND COALESCE("PublishedAt", "CollectedAt") >= $1
            """))
        {
            cmd.Parameters.Add(new() { Value = (now - LoadWindow).ToUniversalTime() });
            await using var r = await cmd.ExecuteReaderAsync(ct);
            while (await r.ReadAsync(ct))
            {
                if (!sources.TryGetValue(r.GetString(1), out var source)) continue; // fonte removida da lista: mantém como está
                var entry = new FeedEntry(r.GetString(2), r.GetString(3), r.GetString(4), r.IsDBNull(5) ? null : r.GetFieldValue<DateTimeOffset>(5));
                var result = Triage.Evaluate(source, entry, now);
                if (!result.Accepted) toDiscard.Add((r.GetGuid(0), result.DiscardReason!));
            }
        }
        if (toDiscard.Count == 0) return 0;
        await using var batch = db.CreateBatch();
        foreach (var (id, reason) in toDiscard)
        {
            var c = new NpgsqlBatchCommand("""UPDATE "FeedItems" SET "Status" = 'descartado', "DiscardReason" = $2, "StoryId" = NULL WHERE "Id" = $1""");
            c.Parameters.Add(new() { Value = id });
            c.Parameters.Add(new() { Value = reason });
            batch.BatchCommands.Add(c);
        }
        await batch.ExecuteNonQueryAsync(ct);
        return toDiscard.Count;
    }

    public async Task<(int Stories, int NewStories, int Corroborated)> GroupAsync(DateTimeOffset now, CancellationToken ct)
    {
        await using var conn = await db.OpenConnectionAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);

        var candidates = new List<ClusterCandidate>();
        var existing = new Dictionary<Guid, Guid>();
        await using (var cmd = new NpgsqlCommand("""
            SELECT "Id", "SourceId", "Title", COALESCE("PublishedAt", "CollectedAt"), "StoryId"
            FROM "FeedItems"
            WHERE "Status" = 'triado' AND COALESCE("PublishedAt", "CollectedAt") >= $1
            FOR UPDATE
            """, conn, tx))
        {
            cmd.Parameters.Add(new() { Value = (now - LoadWindow).ToUniversalTime() });
            await using var r = await cmd.ExecuteReaderAsync(ct);
            while (await r.ReadAsync(ct))
            {
                var id = r.GetGuid(0);
                candidates.Add(new ClusterCandidate(id, r.GetString(1), r.GetString(2), r.GetFieldValue<DateTimeOffset>(3)));
                if (!r.IsDBNull(4)) existing[id] = r.GetGuid(4);
            }
        }

        var assignments = StoryReconciler.Reconcile(StoryClusterer.Cluster(candidates), existing, Guid.NewGuid);
        var touched = assignments.Select(a => a.StoryId).Concat(existing.Values).Distinct().ToArray();

        await using (var batch = new NpgsqlBatch(conn, tx))
        {
            foreach (var a in assignments.Where(a => a.IsNew))
            {
                var c = new NpgsqlBatchCommand("""INSERT INTO "Stories" ("Id", "Title", "Topic", "Status", "ItemCount", "SourceCount", "FirstPublishedAt", "LastPublishedAt", "UpdatedAt") VALUES ($1, '', '', 'agrupada', 0, 0, $2, $2, $2)""");
                c.Parameters.Add(new() { Value = a.StoryId });
                c.Parameters.Add(new() { Value = now.ToUniversalTime() });
                batch.BatchCommands.Add(c);
            }
            foreach (var a in assignments)
            {
                var c = new NpgsqlBatchCommand("""UPDATE "FeedItems" SET "StoryId" = $1 WHERE "Id" = ANY($2) AND "StoryId" IS DISTINCT FROM $1""");
                c.Parameters.Add(new() { Value = a.StoryId });
                c.Parameters.Add(new() { Value = a.ItemIds.ToArray() });
                batch.BatchCommands.Add(c);
            }
            // Recalcula estatísticas e título (manchete mais antiga = quem deu primeiro) das histórias afetadas.
            var stats = new NpgsqlBatchCommand("""
                UPDATE "Stories" s SET
                    "ItemCount" = agg.items, "SourceCount" = agg.sources,
                    "FirstPublishedAt" = agg.first, "LastPublishedAt" = agg.last,
                    "Title" = agg.title, "Topic" = agg.topic, "UpdatedAt" = $2
                FROM (
                    SELECT "StoryId" AS id, COUNT(*) AS items, COUNT(DISTINCT "SourceId") AS sources,
                           MIN(COALESCE("PublishedAt", "CollectedAt")) AS first, MAX(COALESCE("PublishedAt", "CollectedAt")) AS last,
                           (ARRAY_AGG("Title" ORDER BY COALESCE("PublishedAt", "CollectedAt"), "Id"))[1] AS title,
                           MODE() WITHIN GROUP (ORDER BY "Topic") AS topic
                    FROM "FeedItems" WHERE "StoryId" = ANY($1) AND "Status" = 'triado' GROUP BY "StoryId"
                ) agg
                WHERE s."Id" = agg.id
                """);
            stats.Parameters.Add(new() { Value = touched });
            stats.Parameters.Add(new() { Value = now.ToUniversalTime() });
            batch.BatchCommands.Add(stats);
            // História que ficou sem notícia (fusão, ou retriagem descartou todas) é removida.
            batch.BatchCommands.Add(new NpgsqlBatchCommand("""DELETE FROM "Stories" s WHERE NOT EXISTS (SELECT 1 FROM "FeedItems" f WHERE f."StoryId" = s."Id")"""));
            await batch.ExecuteNonQueryAsync(ct);
        }
        await tx.CommitAsync(ct);
        return (assignments.Count, assignments.Count(a => a.IsNew), assignments.Count(a => candidates.Where(c => a.ItemIds.Contains(c.Id)).Select(c => c.SourceId).Distinct().Count() >= 2));
    }
}
