using RadarPlural.Application.Common;
using RadarPlural.Domain.Common;
using RadarPlural.Domain.Ports;
using RadarPlural.Domain.Posts;

namespace RadarPlural.Application.Posts;

// Casos de uso da comunidade de links. O membro vem sempre da sessão (memberId), nunca do corpo da requisição.
public sealed class PostService(IPostRepository posts, IReportRepository reports, IMemberRepository members)
{
    public const string InvalidPost = "Link http(s), título (180), tema (60), tipo válido e comentário (2000) são necessários.";
    public const string InvalidReport = "Informe motivo de até 1000 caracteres.";
    public const string InvalidPage = "Página deve estar entre 1 e 1000000.";
    public const int SavedLimit = 100;

    public async Task<Result<PageDto<PostDto>>> FeedAsync(int page, int pageSize, string? topic, string? type, Guid? viewerId, CancellationToken ct)
    {
        if (!Paging.IsValidPage(page)) return Error.Validation(InvalidPage);
        var result = await posts.ListVisibleAsync(new PostQuery(page, Paging.ClampPageSize(pageSize), topic, type), ct);
        var saved = viewerId is { } id
            ? await posts.SavedAmongAsync(id, result.Items.Select(x => x.Id).ToList(), ct)
            : new HashSet<Guid>();
        return new PageDto<PostDto>(result.Items.Select(p => PostDto.From(p, saved.Contains(p.Id))).ToList(), result.Page, result.PageSize, result.Total);
    }

    public async Task<PageDto<PostDto>> SavedAsync(Guid memberId, CancellationToken ct)
    {
        var items = await posts.ListSavedAsync(memberId, SavedLimit, ct);
        return new PageDto<PostDto>(items.Select(p => PostDto.From(p, true)).ToList(), 1, SavedLimit, items.Count);
    }

    public async Task<Result<PostDto>> CreateAsync(PostInput input, Guid memberId, CancellationToken ct)
    {
        if (!IsValid(input)) return Error.Validation(InvalidPost);
        var author = await members.FindByIdAsync(memberId, ct);
        if (author is null) return Error.Unauthorized();

        var post = new Post
        {
            Url = input.Url!.Trim(),
            Title = input.Title!.Trim(),
            Topic = input.Topic!.Trim(),
            Type = input.Type!,
            Comment = input.Comment?.Trim() ?? "",
            AuthorId = author.Id,
            AuthorName = author.DisplayName
        };
        await posts.AddAsync(post, ct);
        return PostDto.From(post, false);
    }

    public async Task<Result<Unit>> DeleteAsync(Guid postId, Guid memberId, CancellationToken ct)
    {
        var post = await posts.FindAsync(postId, ct);
        if (post is null) return Error.NotFound();
        if (!post.IsAuthoredBy(memberId)) return Error.Forbidden("Só o autor pode excluir a publicação.");
        await posts.RemoveAsync(post, ct); // salvos e denúncias saem junto (cascade no banco)
        return Unit.Value;
    }

    public async Task<Result<Unit>> SaveAsync(Guid postId, Guid memberId, CancellationToken ct)
    {
        if (!await posts.IsVisibleAsync(postId, ct)) return Error.NotFound();
        await posts.SaveForAsync(memberId, postId, ct);
        return Unit.Value;
    }

    public async Task UnsaveAsync(Guid postId, Guid memberId, CancellationToken ct) =>
        await posts.UnsaveForAsync(memberId, postId, ct);

    public async Task<Result<Unit>> ReportAsync(Guid postId, ReportInput input, Guid memberId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(input.Reason) || input.Reason.Length > Report.MaxReasonLength) return Error.Validation(InvalidReport);
        if (!await posts.IsVisibleAsync(postId, ct)) return Error.NotFound();
        await reports.AddAsync(new Report { PostId = postId, MemberId = memberId, Reason = input.Reason.Trim() }, ct);
        return Unit.Value;
    }

    private static bool IsValid(PostInput i) =>
        LinkRules.ValidUrl(i.Url) && i.Url!.Length <= Post.MaxUrlLength
        && !string.IsNullOrWhiteSpace(i.Title) && i.Title.Length <= Post.MaxTitleLength
        && !string.IsNullOrWhiteSpace(i.Topic) && i.Topic.Length <= Post.MaxTopicLength
        && PostTypes.IsValid(i.Type)
        && (i.Comment?.Length ?? 0) <= Post.MaxCommentLength;
}
