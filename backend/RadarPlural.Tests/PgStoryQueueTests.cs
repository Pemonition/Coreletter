using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using RadarPlural.Api;
using RadarPlural.Infrastructure;
using RadarPlural.Ingestion;
using RadarPlural.Pipeline;

namespace RadarPlural.Tests;

// Testes com PostgreSQL de verdade. Rodam quando INTEGRATION_DATABASE_URL aponta para um servidor descartável
// (a CI define; localmente: Host=localhost;Port=5433;Username=...;Password=... do compose). Sem a variável, aparecem como ignorados.
public sealed class IntegrationFactAttribute : FactAttribute
{
    public IntegrationFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("INTEGRATION_DATABASE_URL")))
            Skip = "Defina INTEGRATION_DATABASE_URL (servidor PostgreSQL descartável) para rodar os testes de integração.";
    }
}

// Cria um banco temporário, aplica as migrações reais da API e apaga no fim.
public sealed class PostgresFixture : IAsyncLifetime
{
    public NpgsqlDataSource Db { get; private set; } = null!;
    private string? database;
    private string? adminConnection;

    public async Task InitializeAsync()
    {
        var url = Environment.GetEnvironmentVariable("INTEGRATION_DATABASE_URL");
        if (string.IsNullOrWhiteSpace(url)) return;
        adminConnection = DatabaseUrl.ToNpgsql(url);
        database = $"coreletter_it_{Guid.NewGuid():N}"[..30];
        await using (var admin = new NpgsqlConnection(adminConnection))
        {
            await admin.OpenAsync();
            await using var create = new NpgsqlCommand($"CREATE DATABASE \"{database}\"", admin);
            await create.ExecuteNonQueryAsync();
        }
        var cs = new NpgsqlConnectionStringBuilder(adminConnection) { Database = database }.ConnectionString;
        await using (var ef = new AppDb(new DbContextOptionsBuilder<AppDb>().UseNpgsql(cs).Options)) await ef.Database.MigrateAsync();
        Db = NpgsqlDataSource.Create(cs);
    }

    public async Task DisposeAsync()
    {
        if (database is null) return;
        // Se a criação falhou no meio (servidor fora do ar), não mascara o erro real com outro na limpeza.
        if (Db is not null) await Db.DisposeAsync();
        NpgsqlConnection.ClearAllPools();
        await using var admin = new NpgsqlConnection(adminConnection);
        await admin.OpenAsync();
        await using var drop = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{database}\" WITH (FORCE)", admin);
        await drop.ExecuteNonQueryAsync();
    }

    public async Task<Guid> SeedStoryAsync(string status = StoryStatus.Agrupada, int sources = 2, DateTimeOffset? lastPublished = null)
    {
        var id = Guid.NewGuid();
        await using var cmd = Db.CreateCommand("""
            INSERT INTO "Stories" ("Id", "Title", "Topic", "Status", "ItemCount", "SourceCount", "FirstPublishedAt", "LastPublishedAt", "UpdatedAt", "Attempts", "Version")
            VALUES ($1, 'Teste', 'tecnologia', $2, $3, $3, $4, $4, $4, 0, 1)
            """);
        cmd.Parameters.Add(new() { Value = id });
        cmd.Parameters.Add(new() { Value = status });
        cmd.Parameters.Add(new() { Value = sources });
        cmd.Parameters.Add(new() { Value = (lastPublished ?? DateTimeOffset.UtcNow).ToUniversalTime() });
        await cmd.ExecuteNonQueryAsync();
        return id;
    }

    public async Task<T> ScalarAsync<T>(string sql, params object[] args)
    {
        await using var cmd = Db.CreateCommand(sql);
        foreach (var a in args) cmd.Parameters.Add(new() { Value = a });
        return (T)(await cmd.ExecuteScalarAsync())!;
    }

    public async Task ExecAsync(string sql, params object[] args)
    {
        await using var cmd = Db.CreateCommand(sql);
        foreach (var a in args) cmd.Parameters.Add(new() { Value = a });
        await cmd.ExecuteNonQueryAsync();
    }
}

[CollectionDefinition("postgres")]
public sealed class PostgresCollection : ICollectionFixture<PostgresFixture>;

[Collection("postgres")]
public class PgStoryQueueTests(PostgresFixture pg)
{
    private static readonly StageDefinition Stage = Stages.Auditoria;

    private async Task ResetAsync() => await pg.ExecAsync("""DELETE FROM "FeedItems"; DELETE FROM "Stories";""");

    [IntegrationFact]
    public async Task ConcurrentWorkersNeverClaimTheSameStory()
    {
        await ResetAsync();
        var ids = new List<Guid>();
        for (var i = 0; i < 60; i++) ids.Add(await pg.SeedStoryAsync());
        var queue = new PgStoryQueue(pg.Db);
        var claimed = new ConcurrentBag<Guid>();
        var now = DateTimeOffset.UtcNow;

        // 4 "réplicas" disputando a mesma fila ao mesmo tempo, em lotes pequenos.
        await Task.WhenAll(Enumerable.Range(0, 4).Select(w => Task.Run(async () =>
        {
            while (true)
            {
                var batch = await queue.ClaimAsync(Stage, $"worker-{w}", 3, now, default);
                if (batch.Count == 0) break;
                foreach (var s in batch) claimed.Add(s.StoryId);
            }
        })));

        Assert.Equal(60, claimed.Count);
        Assert.Equal(ids.Order(), claimed.Distinct().Order());
    }

    [IntegrationFact]
    public async Task OnlyCorroboratedStoriesInTheStageInputStateAreClaimed()
    {
        await ResetAsync();
        var ok = await pg.SeedStoryAsync();
        await pg.SeedStoryAsync(sources: 1);                       // fonte única: não gasta IA
        await pg.SeedStoryAsync(status: StoryStatus.Auditada);     // já passou desta etapa
        await pg.SeedStoryAsync(status: StoryStatus.Falhou);
        var claimed = await new PgStoryQueue(pg.Db).ClaimAsync(Stage, "w", 10, DateTimeOffset.UtcNow, default);
        Assert.Equal([ok], claimed.Select(c => c.StoryId));
        Assert.Equal(1, claimed[0].Attempt);
    }

    [IntegrationFact]
    public async Task ExpiredLeaseIsReclaimedAndTheSlowWorkerCannotCompleteAnymore()
    {
        await ResetAsync();
        var id = await pg.SeedStoryAsync();
        var q = new PgStoryQueue(pg.Db);
        var t0 = DateTimeOffset.UtcNow;
        var a = Assert.Single(await q.ClaimAsync(Stage, "lento", 1, t0, default));
        Assert.Empty(await q.ClaimAsync(Stage, "rapido", 1, t0.AddMinutes(1), default)); // lease ainda vale
        var b = Assert.Single(await q.ClaimAsync(Stage, "rapido", 1, t0 + Stage.Lease + TimeSpan.FromSeconds(1), default));
        Assert.Equal(2, b.Attempt);

        Assert.Equal(CompletionResult.LeaseLost, await q.CompleteAsync(Stage, a, "lento", t0.AddMinutes(6), default));
        Assert.Equal(CompletionResult.Completed, await q.CompleteAsync(Stage, b, "rapido", t0.AddMinutes(6), default));
        Assert.Equal(StoryStatus.Auditada, await pg.ScalarAsync<string>("""SELECT "Status" FROM "Stories" WHERE "Id" = $1""", id));
        Assert.Equal(0, await pg.ScalarAsync<int>("""SELECT "Attempts" FROM "Stories" WHERE "Id" = $1""", id));
    }

    [IntegrationFact]
    public async Task RetryWaitsUntilScheduledTime()
    {
        await ResetAsync();
        var id = await pg.SeedStoryAsync();
        var q = new PgStoryQueue(pg.Db);
        var t0 = DateTimeOffset.UtcNow;
        var s = Assert.Single(await q.ClaimAsync(Stage, "w", 1, t0, default));
        Assert.Equal(FailureResult.Retrying, await q.FailAsync(Stage, s, "w", "timeout do provedor", t0.AddMinutes(1), t0, default));
        Assert.Empty(await q.ClaimAsync(Stage, "w", 1, t0.AddSeconds(30), default));
        Assert.Single(await q.ClaimAsync(Stage, "w", 1, t0.AddMinutes(2), default));
        Assert.Equal("timeout do provedor", await pg.ScalarAsync<string>("""SELECT "LastError" FROM "Stories" WHERE "Id" = $1""", id));
    }

    [IntegrationFact]
    public async Task GivingUpMarksTheStoryAsFailed()
    {
        await ResetAsync();
        var id = await pg.SeedStoryAsync();
        var q = new PgStoryQueue(pg.Db);
        var s = Assert.Single(await q.ClaimAsync(Stage, "w", 1, DateTimeOffset.UtcNow, default));
        Assert.Equal(FailureResult.GaveUp, await q.FailAsync(Stage, s, "w", "resposta inválida", null, DateTimeOffset.UtcNow, default));
        Assert.Equal(StoryStatus.Falhou, await pg.ScalarAsync<string>("""SELECT "Status" FROM "Stories" WHERE "Id" = $1""", id));
        Assert.Empty(await q.ClaimAsync(Stage, "w", 1, DateTimeOffset.UtcNow.AddDays(1), default));
    }

    [IntegrationFact]
    public async Task WorkOnAnOutdatedVersionDoesNotAdvanceAndIsReleased()
    {
        await ResetAsync();
        var id = await pg.SeedStoryAsync();
        var q = new PgStoryQueue(pg.Db);
        var t0 = DateTimeOffset.UtcNow;
        var s = Assert.Single(await q.ClaimAsync(Stage, "w", 1, t0, default));
        await pg.ExecAsync("""UPDATE "Stories" SET "Version" = "Version" + 1 WHERE "Id" = $1""", id); // agrupamento juntou notícia nova
        Assert.Equal(CompletionResult.Stale, await q.CompleteAsync(Stage, s, "w", t0, default));
        Assert.Equal(StoryStatus.Agrupada, await pg.ScalarAsync<string>("""SELECT "Status" FROM "Stories" WHERE "Id" = $1""", id));
        var again = Assert.Single(await q.ClaimAsync(Stage, "outro", 1, t0, default)); // liberada na hora, não espera o lease
        Assert.Equal(s.Version + 1, again.Version);
    }

    [IntegrationFact]
    public async Task NewItemInAnAuditedStorySendsItBackToTheStartOfThePipeline()
    {
        await ResetAsync();
        var feed = new FeedItemStore(pg.Db);
        var stories = new StoryStore(pg.Db);
        var src = new FeedSource("fonte-a", "Fonte A", new Uri("https://a.example.org/feed"), "tecnologia");
        var srcB = new FeedSource("fonte-b", "Fonte B", new Uri("https://b.example.org/feed"), "tecnologia");
        var srcC = new FeedSource("fonte-c", "Fonte C", new Uri("https://c.example.org/feed"), "tecnologia");
        var now = DateTimeOffset.UtcNow;
        TriageResult ok = new(true, null, []);
        await feed.SaveAsync(src, [(new FeedEntry("Jensen Huang renuncia ao cargo de CEO da Nvidia", "https://a.example.org/1", "", now.AddHours(-2)), ok)], now, default);
        await feed.SaveAsync(srcB, [(new FeedEntry("Nvidia anuncia saída de Jensen Huang do cargo", "https://b.example.org/1", "", now.AddHours(-1)), ok)], now, default);
        await stories.GroupAsync(now, default);
        var id = await pg.ScalarAsync<Guid>("""SELECT "Id" FROM "Stories" WHERE "SourceCount" = 2""");
        var v1 = await pg.ScalarAsync<int>("""SELECT "Version" FROM "Stories" WHERE "Id" = $1""", id);
        await pg.ExecAsync("""UPDATE "Stories" SET "Status" = 'auditada' WHERE "Id" = $1""", id);

        await stories.GroupAsync(now, default); // nada mudou: continua auditada
        Assert.Equal(StoryStatus.Auditada, await pg.ScalarAsync<string>("""SELECT "Status" FROM "Stories" WHERE "Id" = $1""", id));

        await feed.SaveAsync(srcC, [(new FeedEntry("Ações da Nvidia caem após saída de Jensen Huang", "https://c.example.org/1", "", now), ok)], now, default);
        await stories.GroupAsync(now, default);
        Assert.Equal(StoryStatus.Agrupada, await pg.ScalarAsync<string>("""SELECT "Status" FROM "Stories" WHERE "Id" = $1""", id));
        Assert.Equal(v1 + 1, await pg.ScalarAsync<int>("""SELECT "Version" FROM "Stories" WHERE "Id" = $1""", id));
    }
}

[Collection("postgres")]
public class PgUsageLedgerTests(PostgresFixture pg)
{
    private static readonly RadarPlural.Domain.Billing.Plan Quota10 = new("teste", "Teste", 10m, 10);

    private async Task<Guid> MemberAsync()
    {
        var id = Guid.NewGuid();
        await pg.ExecAsync("""INSERT INTO "Members" ("Id", "Email", "DisplayName", "PasswordHash", "IsAdmin", "PlanId") VALUES ($1, $2, 'Teste', 'x', false, 'plus')""", id, $"{id:N}@example.invalid");
        return id;
    }

    [IntegrationFact]
    public async Task ConcurrentRequestsNeverExceedQuota()
    {
        var member = await MemberAsync();
        var ledger = new PgUsageLedger(pg.Db);
        var now = DateTimeOffset.UtcNow;
        // 50 pedidos simultâneos (várias réplicas da API) contra quota 10.
        var results = await Task.WhenAll(Enumerable.Range(0, 50).Select(_ => Task.Run(() => ledger.ReserveAsync(member, Quota10, Guid.NewGuid(), now, default))));
        Assert.Equal(10, results.Count(r => r == RadarPlural.Domain.Billing.ReserveResult.Reserved));
        Assert.Equal(40, results.Count(r => r == RadarPlural.Domain.Billing.ReserveResult.QuotaExceeded));
        Assert.Equal(10, await ledger.UsedAsync(member, now, default));
    }

    [IntegrationFact]
    public async Task SameKeyIsReservedOnlyOnce()
    {
        var member = await MemberAsync();
        var ledger = new PgUsageLedger(pg.Db);
        var key = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var results = await Task.WhenAll(Enumerable.Range(0, 10).Select(_ => Task.Run(() => ledger.ReserveAsync(member, Quota10, key, now, default))));
        Assert.Equal(1, results.Count(r => r == RadarPlural.Domain.Billing.ReserveResult.Reserved));
        Assert.Equal(9, results.Count(r => r == RadarPlural.Domain.Billing.ReserveResult.AlreadyReserved));
        Assert.Equal(1, await ledger.UsedAsync(member, now, default));
    }

    [IntegrationFact]
    public async Task ReleaseGivesTheUnitBackOnceAndSettleKeepsIt()
    {
        var member = await MemberAsync();
        var ledger = new PgUsageLedger(pg.Db);
        var now = DateTimeOffset.UtcNow;
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        await ledger.ReserveAsync(member, Quota10, a, now, default);
        await ledger.ReserveAsync(member, Quota10, b, now, default);
        await ledger.SettleAsync(a, default);
        await ledger.ReleaseAsync(b, default);
        await ledger.ReleaseAsync(b, default); // repetido: não devolve duas vezes
        await ledger.ReleaseAsync(a, default); // já liquidada: não devolve
        Assert.Equal(1, await ledger.UsedAsync(member, now, default));
    }

    [IntegrationFact]
    public async Task NewMonthStartsFromZeroAndZeroQuotaNeverReserves()
    {
        var member = await MemberAsync();
        var ledger = new PgUsageLedger(pg.Db);
        var nov = new DateTimeOffset(2026, 11, 30, 23, 0, 0, TimeSpan.Zero);
        for (var i = 0; i < 10; i++) await ledger.ReserveAsync(member, Quota10, Guid.NewGuid(), nov, default);
        Assert.Equal(RadarPlural.Domain.Billing.ReserveResult.QuotaExceeded, await ledger.ReserveAsync(member, Quota10, Guid.NewGuid(), nov, default));
        Assert.Equal(RadarPlural.Domain.Billing.ReserveResult.Reserved, await ledger.ReserveAsync(member, Quota10, Guid.NewGuid(), nov.AddHours(2), default));
        Assert.Equal(RadarPlural.Domain.Billing.ReserveResult.QuotaExceeded, await ledger.ReserveAsync(member, Quota10 with { MonthlyComparisons = 0 }, Guid.NewGuid(), nov.AddHours(2), default));
    }
}

[Collection("postgres")]
public class AuditPipelineIntegrationTests(PostgresFixture pg)
{
    [IntegrationFact]
    public async Task CollectedStoryIsAuditedThroughTheQueueAndStored()
    {
        await pg.ExecAsync("""DELETE FROM "FeedItems"; DELETE FROM "Stories";""");
        var feed = new FeedItemStore(pg.Db);
        var now = DateTimeOffset.UtcNow;
        TriageResult ok = new(true, null, []);
        await feed.SaveAsync(new FeedSource("fonte-a", "Fonte A", new Uri("https://a.example.org/f"), "tecnologia"),
            [(new FeedEntry("Vazamento expõe dados de 2 milhões de clientes da Operadora Azul", "https://a.example.org/1", "A empresa confirmou o incidente.", now.AddHours(-2)), ok)], now, default);
        await feed.SaveAsync(new FeedSource("fonte-b", "Fonte B", new Uri("https://b.example.org/f"), "tecnologia"),
            [(new FeedEntry("Operadora Azul admite vazamento de dados de 200 mil clientes", "https://b.example.org/1", "Senhas não foram afetadas, segundo a empresa.", now.AddHours(-1)), ok)], now, default);
        await new StoryStore(pg.Db).GroupAsync(now, default);
        var storyId = await pg.ScalarAsync<Guid>("""SELECT "Id" FROM "Stories" WHERE "SourceCount" = 2""");

        var handler = new AuditStageHandler(new PgStoryReader(pg.Db), new SimulatedAuditor(TimeProvider.System), new PgAuditStore(pg.Db));
        var summary = await new StageRunner(new PgStoryQueue(pg.Db), handler, "w1", TimeProvider.System).RunOnceAsync(10, default);

        Assert.Equal(1, summary.Completed);
        Assert.Equal("auditada", await pg.ScalarAsync<string>("""SELECT "Status" FROM "Stories" WHERE "Id" = $1""", storyId));
        var latest = await new PgAuditStore(pg.Db).LatestAsync(storyId, default);
        Assert.NotNull(latest);
        Assert.Contains(latest!.Value.Result.Claims, c => c.Status == RadarPlural.Pipeline.Comparison.ClaimStatus.Divergente);
        Assert.Equal(2, latest.Value.Result.Sources.Count);
    }
}
