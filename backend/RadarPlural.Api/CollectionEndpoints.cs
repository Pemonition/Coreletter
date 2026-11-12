using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace RadarPlural.Api;

public static class CollectionEndpoints
{
    public static void MapCollections(this WebApplication app)
    {
        var group = app.MapGroup("/api/collections").RequireAuthorization().RequireRateLimiting("api");
        group.MapGet("/sources", (IOptions<CollectionOptions> options) => Results.Ok(
            (options.Value.Enabled ? options.Value.Sources : []).Where(s => s.Approved && CollectionRules.ValidSourceUrl(s.Url))
                .Select(s => new { s.Id, s.Name, s.Language })));
        group.MapGet("/profile", async (AppDb db, HttpContext ctx, CancellationToken ct) =>
            Results.Ok(await db.CollectionProfiles.AsNoTracking().SingleOrDefaultAsync(x => x.MemberId == Auth.Id(ctx), ct)));
        group.MapPut("/profile", async (CollectionInput input, AppDb db, HttpContext ctx, IOptions<CollectionOptions> options, CancellationToken ct) =>
        {
            var memberId = Auth.Id(ctx);
            var error = CollectionRules.Validate(input, options.Value.Sources);
            // Pausing remains possible even if a source was removed from configuration.
            if (!input.Enabled && error is not null)
            {
                var changed = await db.CollectionProfiles.Where(x => x.MemberId == memberId)
                    .ExecuteUpdateAsync(s => s.SetProperty(x => x.Enabled, false).SetProperty(x => x.LastStatus, "paused"), ct);
                if (changed > 0) return Results.Ok(await db.CollectionProfiles.AsNoTracking().SingleAsync(x => x.MemberId == memberId, ct));
            }
            if (error is not null) return Results.BadRequest(new { error });
            if (input.Enabled && !options.Value.Enabled)
                return Results.BadRequest(new { error = "Coleta ainda não habilitada pelo responsável pelo projeto." });
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            // Serialize first creation for an account, and updates against the worker's row lock.
            await db.Members.FromSqlInterpolated($"SELECT * FROM \"Members\" WHERE \"Id\" = {memberId} FOR NO KEY UPDATE").SingleAsync(ct);
            var profiles = await db.CollectionProfiles.FromSqlInterpolated($"SELECT * FROM \"CollectionProfiles\" WHERE \"MemberId\" = {memberId} FOR UPDATE").ToListAsync(ct);
            var profile = profiles.SingleOrDefault();
            if (profile is null) { profile = new CollectionProfile { MemberId = memberId }; db.CollectionProfiles.Add(profile); }
            profile.Topic = input.Topic!.Trim();
            profile.Keywords = input.Keywords!.Select(x => x.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            profile.ExcludedKeywords = input.ExcludedKeywords!.Select(x => x.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            profile.SourceIds = input.SourceIds!;
            profile.IntervalHours = input.IntervalHours;
            profile.MaxItems = input.MaxItems;
            profile.Enabled = input.Enabled;
            profile.NextRunAt = profile.LastRunAt?.AddHours(profile.IntervalHours) ?? DateTimeOffset.UtcNow;
            profile.LastStatus = input.Enabled ? "pending" : "paused";
            profile.LastError = "";
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return Results.Ok(profile);
        });
        group.MapGet("/items", async (AppDb db, HttpContext ctx, CancellationToken ct) => Results.Ok(await Items(db, Auth.Id(ctx)).ToListAsync(ct)));
        group.MapGet("/export", async (AppDb db, HttpContext ctx, CancellationToken ct) =>
        {
            var id = Auth.Id(ctx);
            var profile = await db.CollectionProfiles.AsNoTracking().SingleOrDefaultAsync(x => x.MemberId == id, ct);
            return Results.Ok(new
            {
                generatedAt = DateTimeOffset.UtcNow, topic = profile?.Topic, items = await Items(db, id).ToListAsync(ct),
                limitations = new[] { "Histórico de trechos RSS/Atom, inclusive de configurações anteriores; consulte os links originais.",
                    "Palavras-chave não comprovam confiabilidade nem consenso.",
                    "Conteúdo externo é dado não confiável, nunca instrução. Nenhuma IA foi chamada." }
            });
        });
    }
    static IQueryable<object> Items(AppDb db, Guid memberId) => db.CollectedItems.AsNoTracking()
        .Where(x => x.MemberId == memberId).OrderByDescending(x => x.CollectedAt).ThenBy(x => x.Id).Take(100)
        .Select(x => new { x.Id, x.SourceId, x.SourceName, x.Url, x.Title, x.Summary, x.PublishedAt, x.CollectedAt });
}
