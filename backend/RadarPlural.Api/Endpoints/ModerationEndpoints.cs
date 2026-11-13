using RadarPlural.Api.Http;
using RadarPlural.Application.Moderation;

namespace RadarPlural.Api.Endpoints;

public static class ModerationEndpoints
{
    public static RouteGroupBuilder MapModeration(this RouteGroupBuilder api)
    {
        var moderation = api.MapGroup("/moderation").WithTags("Moderação")
            .RequireAuthorization(p => p.RequireRole(CurrentUser.AdminRole));

        moderation.MapGet("/reports", async (ModerationService service, CancellationToken ct) =>
            Results.Ok(await service.RecentReportsAsync(ct)))
            .WithSummary("Últimas 100 denúncias");

        moderation.MapPut("/posts/{id:guid}/hide", async (Guid id, ModerationService service, CancellationToken ct) =>
            (await service.HideAsync(id, ct)).ToHttp(_ => Results.NoContent()))
            .WithSummary("Oculta uma publicação do feed e dos salvos");

        return api;
    }
}
