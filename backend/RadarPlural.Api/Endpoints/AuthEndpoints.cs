using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using RadarPlural.Api.Http;
using RadarPlural.Application.Auth;

namespace RadarPlural.Api.Endpoints;

public static class AuthEndpoints
{
    public static RouteGroupBuilder MapAuth(this RouteGroupBuilder api)
    {
        var auth = api.MapGroup("/auth").WithTags("Conta").RequireRateLimiting(RateLimits.Api);

        auth.MapPost("/register", async (RegisterInput input, AuthService service, HttpContext ctx, CancellationToken ct) =>
        {
            var result = await service.RegisterAsync(input, ct);
            if (result.IsSuccess) await SignInAsync(ctx, result.Value!);
            return result.ToHttp(Results.Ok);
        }).WithSummary("Cria conta e já inicia a sessão");

        auth.MapPost("/login", async (LoginInput input, AuthService service, HttpContext ctx, CancellationToken ct) =>
        {
            var result = await service.LoginAsync(input, ct);
            if (result.IsSuccess) await SignInAsync(ctx, result.Value!);
            return result.ToHttp(Results.Ok);
        }).WithSummary("Inicia sessão (cookie HttpOnly)");

        auth.MapPost("/logout", async (HttpContext ctx) =>
        {
            await ctx.SignOutAsync();
            return Results.NoContent();
        }).RequireAuthorization().WithSummary("Encerra a sessão");

        auth.MapGet("/me", async (ClaimsPrincipal user, AuthService service, CancellationToken ct) =>
            (await service.CurrentAsync(user.MemberId(), ct)).ToHttp(Results.Ok))
            .RequireAuthorization().WithSummary("Usuário da sessão atual (401 para visitante)");

        return api;
    }

    // A sessão é um detalhe do adapter web: o caso de uso só confirma quem é o usuário.
    private static Task SignInAsync(HttpContext ctx, UserDto user)
    {
        var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, user.Id.ToString()), new(ClaimTypes.Name, user.DisplayName) };
        if (user.IsAdmin) claims.Add(new(ClaimTypes.Role, CurrentUser.AdminRole));
        return ctx.SignInAsync(new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme)));
    }
}
