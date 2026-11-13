using System.Security.Claims;

namespace RadarPlural.Api.Http;

public static class CurrentUser
{
    public const string AdminRole = "admin";

    // Identidade sempre lida do cookie assinado, nunca de ID enviado no corpo ou na URL.
    public static Guid MemberId(this ClaimsPrincipal user) => Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);

    public static Guid? MemberIdOrNull(this ClaimsPrincipal user) =>
        user.Identity?.IsAuthenticated == true ? user.MemberId() : null;
}
