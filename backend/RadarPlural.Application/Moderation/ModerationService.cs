using RadarPlural.Application.Common;
using RadarPlural.Domain.Ports;

namespace RadarPlural.Application.Moderation;

public sealed record ReportDto(Guid Id, Guid PostId, string Reason, DateTimeOffset CreatedAt);

// Quem pode moderar é decidido no adapter web (papel "admin" na sessão); aqui ficam as ações.
public sealed class ModerationService(IReportRepository reports, IPostRepository posts)
{
    public const int ReportLimit = 100;

    public async Task<IReadOnlyList<ReportDto>> RecentReportsAsync(CancellationToken ct) =>
        (await reports.ListRecentAsync(ReportLimit, ct)).Select(r => new ReportDto(r.Id, r.PostId, r.Reason, r.CreatedAt)).ToList();

    public async Task<Result<Unit>> HideAsync(Guid postId, CancellationToken ct)
    {
        var post = await posts.FindAsync(postId, ct);
        if (post is null) return Error.NotFound();
        await posts.HideAsync(post, ct);
        return Unit.Value;
    }
}
