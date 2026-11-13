using Microsoft.EntityFrameworkCore;
using RadarPlural.Domain.Common;
using RadarPlural.Domain.News;
using RadarPlural.Domain.Ports;

namespace RadarPlural.Infrastructure.Persistence.Repositories;

public sealed class NewsRepository(AppDb db) : INewsRepository
{
    public async Task<PagedResult<FeedItem>> ListAsync(NewsQuery query, CancellationToken ct)
    {
        var q = db.FeedItems.AsNoTracking();
        if (query.Topic is not null) q = q.Where(x => x.Topic == query.Topic);
        if (query.SourceId is not null) q = q.Where(x => x.SourceId == query.SourceId);
        var total = await q.CountAsync(ct);
        var items = await q.OrderByDescending(x => x.PublishedAt ?? x.CollectedAt).ThenByDescending(x => x.Id)
            .Skip((query.Page - 1) * query.PageSize).Take(query.PageSize).ToListAsync(ct);
        return new PagedResult<FeedItem>(items, query.Page, query.PageSize, total);
    }
}
