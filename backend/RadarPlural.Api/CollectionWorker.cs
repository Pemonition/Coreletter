using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace RadarPlural.Api;

public sealed class CollectionWorker(IServiceScopeFactory scopes, IOptions<CollectionOptions> options,
    ILogger<CollectionWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.Enabled) return;
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopes.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDb>();
                await RunOne(db, scope.ServiceProvider.GetRequiredService<IFeedFetcher>(), options.Value, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex) { logger.LogWarning("Coleta indisponível ({Type}); confira banco e configuração.", ex.GetType().Name); }
            try { await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
        }
    }
    public static async Task<bool> RunOne(AppDb db, IFeedFetcher fetcher, CollectionOptions options, CancellationToken ct)
    {
        if (!options.Enabled) return false;
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var now = DateTimeOffset.UtcNow;
        var profiles = await db.CollectionProfiles.FromSqlInterpolated($"SELECT * FROM \"CollectionProfiles\" WHERE \"Enabled\" = TRUE AND \"NextRunAt\" <= {now} ORDER BY \"NextRunAt\" LIMIT 1 FOR UPDATE SKIP LOCKED").ToListAsync(ct);
        var p = profiles.SingleOrDefault();
        if (p is null) return false;
        var input = p.Input();
        var error = CollectionRules.Validate(input, options.Sources);
        var failures = new List<string>();
        var successes = 0;
        var added = 0;
        if (error is not null) failures.Add("Configuração inválida; revise as fontes e o questionário.");
        else
        {
            var seen = (await db.CollectedItems.Where(x => x.MemberId == p.MemberId).Select(x => x.Url).ToListAsync(ct)).ToHashSet();
            foreach (var id in p.SourceIds)
            {
                var source = options.Sources.Single(x => x.Id == id);
                try
                {
                    var entries = await fetcher.Fetch(source, ct);
                    successes++;
                    foreach (var e in entries.Where(e => CollectionRules.Matches(e, input)))
                    {
                        if (added >= p.MaxItems) break;
                        if (!seen.Add(e.Url)) continue;
                        db.CollectedItems.Add(new CollectedItem { MemberId = p.MemberId, SourceId = source.Id,
                            SourceName = source.Name, Url = e.Url, Title = e.Title, Summary = e.Summary,
                            PublishedAt = e.PublishedAt, CollectedAt = now });
                        added++;
                    }
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
                catch (Exception ex) when (ex is HttpRequestException or IOException or OperationCanceledException or System.Xml.XmlException or System.Text.RegularExpressions.RegexMatchTimeoutException)
                { failures.Add($"Fonte {source.Id}: coleta indisponível. Revise o endereço, o formato e a permissão."); }
            }
        }
        p.LastRunAt = now;
        p.NextRunAt = now.AddHours(p.IntervalHours);
        p.LastStatus = successes == 0 ? "failed" : failures.Count > 0 ? "partial" : "success";
        p.LastError = string.Join(" ", failures);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return true;
    }
}
