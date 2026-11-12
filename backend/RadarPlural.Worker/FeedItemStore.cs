using Npgsql;
using RadarPlural.Ingestion;

namespace RadarPlural.Worker;

// Grava no esquema criado pelas migrações da API (tabela "FeedItems"); o Worker não altera o esquema.
// ON CONFLICT ("Url") DO NOTHING: rodar o coletor de novo nunca duplica notícia.
public sealed class FeedItemStore(NpgsqlDataSource db)
{
    public async Task<int> SaveAsync(FeedSource source, IReadOnlyList<FeedEntry> entries, DateTimeOffset collectedAt, CancellationToken ct)
    {
        if (entries.Count == 0) return 0;
        await using var batch = db.CreateBatch();
        foreach (var e in entries)
        {
            var cmd = new NpgsqlBatchCommand("""
                INSERT INTO "FeedItems" ("Id", "SourceId", "SourceName", "Topic", "Url", "Title", "Summary", "PublishedAt", "CollectedAt")
                VALUES ($1, $2, $3, $4, $5, $6, $7, $8, $9)
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
            batch.BatchCommands.Add(cmd);
        }
        return await batch.ExecuteNonQueryAsync(ct);
    }
}
