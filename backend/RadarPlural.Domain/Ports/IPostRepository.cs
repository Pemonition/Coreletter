using RadarPlural.Domain.Common;
using RadarPlural.Domain.Posts;

namespace RadarPlural.Domain.Ports;

public sealed record PostQuery(int Page, int PageSize, string? Topic, string? Type);

public interface IPostRepository
{
    // Somente publicações visíveis (não ocultadas pela moderação).
    Task<PagedResult<Post>> ListVisibleAsync(PostQuery query, CancellationToken ct);
    Task<IReadOnlyList<Post>> ListSavedAsync(Guid memberId, int limit, CancellationToken ct);
    Task<IReadOnlySet<Guid>> SavedAmongAsync(Guid memberId, IReadOnlyCollection<Guid> postIds, CancellationToken ct);

    Task<Post?> FindAsync(Guid id, CancellationToken ct);
    Task<bool> IsVisibleAsync(Guid id, CancellationToken ct);
    Task AddAsync(Post post, CancellationToken ct);
    Task RemoveAsync(Post post, CancellationToken ct);
    Task HideAsync(Post post, CancellationToken ct);

    // Idempotente: salvar duas vezes mantém um único registro.
    Task SaveForAsync(Guid memberId, Guid postId, CancellationToken ct);
    Task UnsaveForAsync(Guid memberId, Guid postId, CancellationToken ct);
}
