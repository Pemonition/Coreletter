using Microsoft.EntityFrameworkCore;
using RadarPlural.Domain.Common;
using RadarPlural.Domain.Ports;
using RadarPlural.Domain.Posts;

namespace RadarPlural.Infrastructure.Persistence.Repositories;

public sealed class PostRepository(AppDb db) : IPostRepository
{
    public async Task<PagedResult<Post>> ListVisibleAsync(PostQuery query, CancellationToken ct)
    {
        var q = db.Posts.AsNoTracking().Where(x => !x.Hidden);
        if (!string.IsNullOrWhiteSpace(query.Topic))
        {
            var topic = query.Topic.ToLower();
            q = q.Where(x => x.Topic.ToLower().Contains(topic));
        }
        if (!string.IsNullOrWhiteSpace(query.Type)) q = q.Where(x => x.Type == query.Type);

        var total = await q.CountAsync(ct);
        // Desempate por Id: sem ele, itens com o mesmo CreatedAt podem se repetir ou sumir entre páginas.
        var items = await q.OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id)
            .Skip((query.Page - 1) * query.PageSize).Take(query.PageSize).ToListAsync(ct);
        return new PagedResult<Post>(items, query.Page, query.PageSize, total);
    }

    public async Task<IReadOnlyList<Post>> ListSavedAsync(Guid memberId, int limit, CancellationToken ct) =>
        await db.Posts.AsNoTracking()
            .Where(x => !x.Hidden && db.Saved.Any(s => s.PostId == x.Id && s.MemberId == memberId))
            .OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id)
            .Take(limit).ToListAsync(ct);

    public async Task<IReadOnlySet<Guid>> SavedAmongAsync(Guid memberId, IReadOnlyCollection<Guid> postIds, CancellationToken ct) =>
        (await db.Saved.Where(x => x.MemberId == memberId && postIds.Contains(x.PostId)).Select(x => x.PostId).ToListAsync(ct)).ToHashSet();

    public async Task<Post?> FindAsync(Guid id, CancellationToken ct) => await db.Posts.FindAsync([id], ct);

    public Task<bool> IsVisibleAsync(Guid id, CancellationToken ct) => db.Posts.AnyAsync(x => x.Id == id && !x.Hidden, ct);

    public async Task AddAsync(Post post, CancellationToken ct)
    {
        db.Posts.Add(post);
        await db.SaveChangesAsync(ct);
    }

    public async Task RemoveAsync(Post post, CancellationToken ct)
    {
        db.Posts.Remove(post);
        await db.SaveChangesAsync(ct);
    }

    public async Task HideAsync(Post post, CancellationToken ct)
    {
        post.Hidden = true;
        await db.SaveChangesAsync(ct);
    }

    // ON CONFLICT DO NOTHING: idempotente mesmo com dois cliques simultâneos.
    public async Task SaveForAsync(Guid memberId, Guid postId, CancellationToken ct) =>
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO \"Saved\" (\"MemberId\", \"PostId\") VALUES ({memberId}, {postId}) ON CONFLICT DO NOTHING", ct);

    public async Task UnsaveForAsync(Guid memberId, Guid postId, CancellationToken ct) =>
        await db.Saved.Where(x => x.PostId == postId && x.MemberId == memberId).ExecuteDeleteAsync(ct);
}
