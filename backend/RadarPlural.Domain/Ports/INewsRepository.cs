using RadarPlural.Domain.Common;
using RadarPlural.Domain.News;

namespace RadarPlural.Domain.Ports;

public sealed record NewsQuery(int Page, int PageSize, string? Topic, string? SourceId);

public interface INewsRepository
{
    // Ordem: publicação (ou coleta, se a fonte não informou data) mais recente primeiro.
    Task<PagedResult<FeedItem>> ListAsync(NewsQuery query, CancellationToken ct);
}
