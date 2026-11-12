using Microsoft.EntityFrameworkCore;
using Npgsql;
using RadarPlural.Api;

// Opt in only with a disposable fixture database; never point at the course's real data.
public sealed class CollectionPostgresFactAttribute : FactAttribute
{
    public CollectionPostgresFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("CORELETTER_TEST_POSTGRES")))
            Skip = "Defina CORELETTER_TEST_POSTGRES para PostgreSQL descartável.";
    }
}
public class CollectionPostgresTests
{
    static readonly FeedSource Good = new("ok", "Teste", "https://news.example.com/feed", "pt", true);
    [CollectionPostgresFact]
    public async Task WorkerPersistsDeduplicatesIsolatesAndHonorsPause()
    {
        await using var f = await Fixture.Create();
        var a = await f.AddMember(); var b = await f.AddMember();
        var p = new CollectionProfile { MemberId = a.Id, Topic = "IA", Keywords = ["IA"], SourceIds = ["ok"], Enabled = true, NextRunAt = DateTimeOffset.UtcNow.AddMinutes(-1) };
        f.Db.CollectionProfiles.Add(p); await f.Db.SaveChangesAsync();
        var fetch = new Fake(false); var options = new CollectionOptions { Enabled = true, Sources = [Good] };
        Assert.True(await CollectionWorker.RunOne(f.Db, fetch, options, default));
        Assert.Equal("success", p.LastStatus);
        Assert.Single(await f.Db.CollectedItems.Where(x => x.MemberId == a.Id).ToListAsync());
        Assert.Empty(await f.Db.CollectedItems.Where(x => x.MemberId == b.Id).ToListAsync());
        p.NextRunAt = DateTimeOffset.UtcNow.AddMinutes(-1); await f.Db.SaveChangesAsync();
        Assert.True(await CollectionWorker.RunOne(f.Db, fetch, options, default));
        Assert.Equal(1, await f.Db.CollectedItems.CountAsync());
        p.Enabled = false; p.NextRunAt = DateTimeOffset.UtcNow.AddMinutes(-1); await f.Db.SaveChangesAsync();
        Assert.False(await CollectionWorker.RunOne(f.Db, fetch, options, default));
    }
    [CollectionPostgresFact]
    public async Task BrokenSourceIsFailedAndNextRunMovesForward()
    {
        await using var f = await Fixture.Create();
        var member = await f.AddMember();
        var p = new CollectionProfile { MemberId = member.Id, Topic = "IA", Keywords = ["IA"], SourceIds = ["ok"], Enabled = true, NextRunAt = DateTimeOffset.UtcNow.AddMinutes(-1) };
        f.Db.CollectionProfiles.Add(p); await f.Db.SaveChangesAsync();
        Assert.True(await CollectionWorker.RunOne(f.Db, new Fake(true), new CollectionOptions { Enabled = true, Sources = [Good] }, default));
        Assert.Equal("failed", p.LastStatus);
        Assert.True(p.NextRunAt > DateTimeOffset.UtcNow);
        Assert.NotEmpty(p.LastError);
        Assert.Empty(await f.Db.CollectedItems.ToListAsync());
    }
    [CollectionPostgresFact]
    public async Task LockedProfileIsSkippedBySecondWorker()
    {
        await using var f = await Fixture.Create();
        var member = await f.AddMember();
        var p = new CollectionProfile { MemberId = member.Id, Topic = "IA", Keywords = ["IA"], SourceIds = ["ok"], Enabled = true, NextRunAt = DateTimeOffset.UtcNow.AddMinutes(-1) };
        f.Db.CollectionProfiles.Add(p); await f.Db.SaveChangesAsync();
        await using var tx = await f.Db.Database.BeginTransactionAsync();
        await f.Db.CollectionProfiles.FromSqlRaw("SELECT * FROM \"CollectionProfiles\" FOR UPDATE").ToListAsync();
        await using var second = new AppDb(new DbContextOptionsBuilder<AppDb>().UseNpgsql(f.ConnectionString).Options);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        Assert.False(await CollectionWorker.RunOne(second, new Fake(false), new CollectionOptions { Enabled = true, Sources = [Good] }, timeout.Token));
        await tx.RollbackAsync();
    }
    sealed class Fake(bool fail) : IFeedFetcher
    {
        public Task<IReadOnlyList<FeedEntry>> Fetch(FeedSource s, CancellationToken ct)
        {
            if (fail) throw new IOException("Fixture stream interrupted");
            return Task.FromResult<IReadOnlyList<FeedEntry>>([new("https://news.example.com/a", "IA na indústria", "Estudo", null)]);
        }
    }
    sealed class Fixture(AppDb db, string cs, string schema) : IAsyncDisposable
    {
        public AppDb Db => db;
        public string ConnectionString => cs;
        public static async Task<Fixture> Create()
        {
            var csb = new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable("CORELETTER_TEST_POSTGRES"));
            if (csb.Database is null || !csb.Database.StartsWith("coreletter_test", StringComparison.Ordinal))
                throw new InvalidOperationException("Use uma base descartável com prefixo coreletter_test.");
            var schema = "fixture_" + Guid.NewGuid().ToString("N");
            csb.SearchPath = schema;
            var db = new AppDb(new DbContextOptionsBuilder<AppDb>().UseNpgsql(csb.ConnectionString).Options);
            // Identifier is generated from a GUID above, never caller input; SQL parameters cannot name schemas.
#pragma warning disable EF1002
            await db.Database.ExecuteSqlRawAsync($"CREATE SCHEMA \"{schema}\"");
#pragma warning restore EF1002
            await db.Database.ExecuteSqlRawAsync("CREATE TABLE \"Members\" (\"Id\" uuid PRIMARY KEY, \"Email\" text NOT NULL, \"DisplayName\" text NOT NULL, \"PasswordHash\" text NOT NULL, \"IsAdmin\" boolean NOT NULL)");
            await db.Database.ExecuteSqlRawAsync(CollectionData.Schema);
            return new Fixture(db, csb.ConnectionString, schema);
        }
        public async Task<Member> AddMember()
        {
            var m = new Member { Email = Guid.NewGuid() + "@example.invalid", DisplayName = "Fixture" };
            db.Members.Add(m); await db.SaveChangesAsync(); return m;
        }
        public async ValueTask DisposeAsync()
        {
            // Only the random schema created by this test is removed; no shared or application tables.
#pragma warning disable EF1002 // Private identifier generated from a GUID in Create(), never external input.
            await db.Database.ExecuteSqlRawAsync($"DROP SCHEMA \"{schema}\" CASCADE");
#pragma warning restore EF1002
            await db.DisposeAsync();
        }
    }
}
