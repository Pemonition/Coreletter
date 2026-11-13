using RadarPlural.Application.Common;
using RadarPlural.Application.Posts;
using RadarPlural.Domain.Common;
using RadarPlural.Domain.News;
using RadarPlural.Domain.Ports;

namespace RadarPlural.Application.News;

public sealed record NewsItemDto(Guid Id, string SourceId, string SourceName, string Topic, string Url, string Title, string Summary, DateTimeOffset? PublishedAt, DateTimeOffset CollectedAt)
{
    public static NewsItemDto From(FeedItem x) => new(x.Id, x.SourceId, x.SourceName, x.Topic, x.Url, x.Title, x.Summary, x.PublishedAt, x.CollectedAt);
}

// Leitura das notícias que o RadarPlural.Worker coletou das fontes aprovadas.
public sealed class NewsService(INewsRepository news)
{
    public async Task<Result<PageDto<NewsItemDto>>> ListAsync(int page, int pageSize, string? topic, string? source, CancellationToken ct)
    {
        if (!Paging.IsValidPage(page)) return Error.Validation(PostService.InvalidPage);
        var query = new NewsQuery(page, Paging.ClampPageSize(pageSize),
            string.IsNullOrWhiteSpace(topic) ? null : topic.Trim().ToLowerInvariant(),
            string.IsNullOrWhiteSpace(source) ? null : source.Trim());
        var result = await news.ListAsync(query, ct);
        return new PageDto<NewsItemDto>(result.Items.Select(NewsItemDto.From).ToList(), result.Page, result.PageSize, result.Total);
    }
}
