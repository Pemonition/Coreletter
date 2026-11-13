using System.Security.Claims;
using RadarPlural.Api.Http;
using RadarPlural.Application.Posts;

namespace RadarPlural.Api.Endpoints;

public static class PostEndpoints
{
    public static RouteGroupBuilder MapPosts(this RouteGroupBuilder api)
    {
        var posts = api.MapGroup("/posts").WithTags("Publicações");

        // Feed público: sem rate limit para não penalizar leitura (comportamento original).
        posts.MapGet("", async (PostService service, ClaimsPrincipal user, CancellationToken ct, int page = 1, int pageSize = 20, string? topic = null, string? type = null) =>
            (await service.FeedAsync(page, pageSize, topic, type, user.MemberIdOrNull(), ct)).ToHttp(Results.Ok))
            .WithSummary("Feed público paginado, com filtros por tema e tipo");

        var member = posts.MapGroup("").RequireAuthorization().RequireRateLimiting(RateLimits.Api);

        member.MapGet("/saved", async (PostService service, ClaimsPrincipal user, CancellationToken ct) =>
            Results.Ok(await service.SavedAsync(user.MemberId(), ct)))
            .WithSummary("Itens salvos da própria conta");

        member.MapPost("", async (PostInput input, PostService service, ClaimsPrincipal user, CancellationToken ct) =>
            (await service.CreateAsync(input, user.MemberId(), ct)).ToHttp(p => Results.Created($"/api/posts/{p.Id}", p)))
            .WithSummary("Publica um link (article, podcast ou music)");

        member.MapDelete("/{id:guid}", async (Guid id, PostService service, ClaimsPrincipal user, CancellationToken ct) =>
            (await service.DeleteAsync(id, user.MemberId(), ct)).ToHttp(_ => Results.NoContent()))
            .WithSummary("Exclui a própria publicação");

        member.MapPut("/{id:guid}/save", async (Guid id, PostService service, ClaimsPrincipal user, CancellationToken ct) =>
            (await service.SaveAsync(id, user.MemberId(), ct)).ToHttp(_ => Results.NoContent()))
            .WithSummary("Salva (idempotente)");

        member.MapDelete("/{id:guid}/save", async (Guid id, PostService service, ClaimsPrincipal user, CancellationToken ct) =>
        {
            await service.UnsaveAsync(id, user.MemberId(), ct);
            return Results.NoContent();
        }).WithSummary("Remove dos salvos");

        member.MapPost("/{id:guid}/reports", async (Guid id, ReportInput input, PostService service, ClaimsPrincipal user, CancellationToken ct) =>
            (await service.ReportAsync(id, input, user.MemberId(), ct)).ToHttp(_ => Results.NoContent()))
            .WithSummary("Denuncia uma publicação");

        return api;
    }
}
