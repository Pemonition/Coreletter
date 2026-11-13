using RadarPlural.Domain.Common;
using RadarPlural.Domain.Members;
using RadarPlural.Domain.Ports;
using RadarPlural.Domain.Posts;

namespace RadarPlural.Tests.Application;

// Adapters em memória para as portas do domínio. Mesma interface que o EF Core implementa:
// os casos de uso rodam sem banco, em milissegundos. O comportamento com PostgreSQL real fica em RadarPlural.IntegrationTests.
internal sealed class InMemoryMembers : IMemberRepository
{
    public readonly List<Member> Items = [];

    public Task<Member?> FindByIdAsync(Guid id, CancellationToken ct) => Task.FromResult(Items.SingleOrDefault(x => x.Id == id));
    public Task<Member?> FindByEmailAsync(string email, CancellationToken ct) => Task.FromResult(Items.SingleOrDefault(x => x.Email == email));
    public Task<bool> EmailExistsAsync(string email, CancellationToken ct) => Task.FromResult(Items.Any(x => x.Email == email));

    public Task<bool> TryAddAsync(Member member, CancellationToken ct)
    {
        if (Items.Any(x => x.Email == member.Email)) return Task.FromResult(false);
        Items.Add(member);
        return Task.FromResult(true);
    }
}

internal sealed class InMemoryPosts : IPostRepository
{
    public readonly List<Post> Items = [];
    public readonly HashSet<(Guid MemberId, Guid PostId)> Saved = [];

    public Task<PagedResult<Post>> ListVisibleAsync(PostQuery q, CancellationToken ct)
    {
        var visible = Items.Where(x => !x.Hidden).OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id).ToList();
        return Task.FromResult(new PagedResult<Post>(visible.Skip((q.Page - 1) * q.PageSize).Take(q.PageSize).ToList(), q.Page, q.PageSize, visible.Count));
    }

    public Task<IReadOnlyList<Post>> ListSavedAsync(Guid memberId, int limit, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<Post>>(Items.Where(p => !p.Hidden && Saved.Contains((memberId, p.Id))).Take(limit).ToList());

    public Task<IReadOnlySet<Guid>> SavedAmongAsync(Guid memberId, IReadOnlyCollection<Guid> ids, CancellationToken ct) =>
        Task.FromResult<IReadOnlySet<Guid>>(ids.Where(id => Saved.Contains((memberId, id))).ToHashSet());

    public Task<Post?> FindAsync(Guid id, CancellationToken ct) => Task.FromResult(Items.SingleOrDefault(x => x.Id == id));
    public Task<bool> IsVisibleAsync(Guid id, CancellationToken ct) => Task.FromResult(Items.Any(x => x.Id == id && !x.Hidden));
    public Task AddAsync(Post post, CancellationToken ct) { Items.Add(post); return Task.CompletedTask; }
    public Task RemoveAsync(Post post, CancellationToken ct) { Items.Remove(post); Saved.RemoveWhere(s => s.PostId == post.Id); return Task.CompletedTask; }
    public Task HideAsync(Post post, CancellationToken ct) { post.Hidden = true; return Task.CompletedTask; }
    public Task SaveForAsync(Guid memberId, Guid postId, CancellationToken ct) { Saved.Add((memberId, postId)); return Task.CompletedTask; }
    public Task UnsaveForAsync(Guid memberId, Guid postId, CancellationToken ct) { Saved.Remove((memberId, postId)); return Task.CompletedTask; }
}

internal sealed class InMemoryReports : IReportRepository
{
    public readonly List<Report> Items = [];
    public Task AddAsync(Report report, CancellationToken ct) { Items.Add(report); return Task.CompletedTask; }
    public Task<IReadOnlyList<Report>> ListRecentAsync(int limit, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<Report>>(Items.OrderByDescending(x => x.CreatedAt).Take(limit).ToList());
}

// Hash falso e legível: o teste verifica a regra, não o algoritmo (o algoritmo real tem teste próprio).
internal sealed class FakeHasher : IPasswordHasher
{
    public string Hash(Member member, string password) => "hash:" + password;
    public bool Verify(Member member, string password) => member.PasswordHash == "hash:" + password;
}
