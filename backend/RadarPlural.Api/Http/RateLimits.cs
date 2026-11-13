using System.Threading.RateLimiting;

namespace RadarPlural.Api.Http;

public static class RateLimits
{
    public const string Api = "api";

    // Janela fixa por IP. Valor configurável (RateLimit__PermitLimit); os testes de integração sobem o limite.
    public static IServiceCollection AddApiRateLimiting(this IServiceCollection services, IConfiguration configuration)
    {
        var permitLimit = configuration.GetValue("RateLimit:PermitLimit", 60);
        return services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddPolicy(Api, context => RateLimitPartition.GetFixedWindowLimiter(
                context.Connection.RemoteIpAddress?.ToString() ?? "local",
                _ => new FixedWindowRateLimiterOptions { PermitLimit = permitLimit, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
        });
    }
}
