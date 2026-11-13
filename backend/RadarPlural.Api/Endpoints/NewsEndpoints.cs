using RadarPlural.Api.Http;
using RadarPlural.Application.News;

namespace RadarPlural.Api.Endpoints;

public static class NewsEndpoints
{
    public static RouteGroupBuilder MapNews(this RouteGroupBuilder api)
    {
        api.MapGet("/news", async (NewsService service, CancellationToken ct, int page = 1, int pageSize = 20, string? topic = null, string? source = null) =>
            (await service.ListAsync(page, pageSize, topic, source, ct)).ToHttp(Results.Ok))
            .WithTags("Notícias").RequireRateLimiting(RateLimits.Api)
            .WithSummary("Notícias coletadas das fontes aprovadas (RadarPlural.Worker)");
        return api;
    }
}
