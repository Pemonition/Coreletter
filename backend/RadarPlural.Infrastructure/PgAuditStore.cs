using System.Text.Json;
using Npgsql;
using NpgsqlTypes;
using RadarPlural.Pipeline;
using RadarPlural.Pipeline.Comparison;

namespace RadarPlural.Infrastructure;

// Lê a história com as notícias aprovadas na triagem (texto já higienizado pelo coletor).
public sealed class PgStoryReader(NpgsqlDataSource db) : IStoryReader
{
    public async Task<StorySnapshot?> ReadAsync(Guid storyId, CancellationToken ct)
    {
        await using var cmd = db.CreateCommand("""
            SELECT s."Version", f."Id", f."SourceId", f."SourceName", f."Url", f."Title", f."Summary", f."PublishedAt"
            FROM "Stories" s LEFT JOIN "FeedItems" f ON f."StoryId" = s."Id" AND f."Status" = 'triado'
            WHERE s."Id" = $1
            ORDER BY f."PublishedAt" NULLS LAST, f."Id"
            """);
        cmd.Parameters.Add(new() { Value = storyId });
        await using var r = await cmd.ExecuteReaderAsync(ct);
        int? version = null;
        var items = new List<StoryItem>();
        while (await r.ReadAsync(ct))
        {
            version = r.GetInt32(0);
            if (r.IsDBNull(1)) continue;
            items.Add(new StoryItem(r.GetGuid(1), r.GetString(2), r.GetString(3), r.GetString(4), r.GetString(5), r.GetString(6),
                r.IsDBNull(7) ? null : r.GetFieldValue<DateTimeOffset>(7)));
        }
        return version is null ? null : new StorySnapshot(storyId, version.Value, items);
    }
}

// Grava cada comparação (histórico, com a versão da história). A mais recente é a que a API mostra.
public sealed class PgAuditStore(NpgsqlDataSource db) : IAuditStore
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task SaveAsync(ComparisonResult result, int storyVersion, CancellationToken ct)
    {
        await using var cmd = db.CreateCommand("""
            INSERT INTO "StoryAudits" ("Id", "StoryId", "StoryVersion", "Provider", "Simulated", "Result", "CreatedAt")
            VALUES ($1, $2, $3, $4, $5, $6, $7)
            """);
        cmd.Parameters.Add(new() { Value = Guid.NewGuid() });
        cmd.Parameters.Add(new() { Value = result.StoryId });
        cmd.Parameters.Add(new() { Value = storyVersion });
        cmd.Parameters.Add(new() { Value = result.Provider });
        cmd.Parameters.Add(new() { Value = result.Simulated });
        cmd.Parameters.Add(new() { Value = JsonSerializer.Serialize(result, Json), NpgsqlDbType = NpgsqlDbType.Jsonb });
        cmd.Parameters.Add(new() { Value = result.CreatedAt.ToUniversalTime() });
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<(ComparisonResult Result, int StoryVersion)?> LatestAsync(Guid storyId, CancellationToken ct)
    {
        await using var cmd = db.CreateCommand("""
            SELECT "Result"::text, "StoryVersion" FROM "StoryAudits" WHERE "StoryId" = $1 ORDER BY "CreatedAt" DESC, "Id" LIMIT 1
            """);
        cmd.Parameters.Add(new() { Value = storyId });
        await using var r = await cmd.ExecuteReaderAsync(ct);
        if (!await r.ReadAsync(ct)) return null;
        return (JsonSerializer.Deserialize<ComparisonResult>(r.GetString(0), Json)!, r.GetInt32(1));
    }
}
