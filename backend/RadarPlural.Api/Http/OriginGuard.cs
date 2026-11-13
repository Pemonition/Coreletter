namespace RadarPlural.Api.Http;

// Defesa contra CSRF: rejeita mutações (POST/PUT/DELETE) vindas de outra origem no navegador.
// Complementa o cookie SameSite=Strict; funciona porque front e API ficam na mesma origem.
public sealed class OriginGuard(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        if (!HttpMethods.IsGet(context.Request.Method) && !HttpMethods.IsHead(context.Request.Method) && !HttpMethods.IsOptions(context.Request.Method)
            && context.Request.Headers.TryGetValue("Origin", out var origins)
            && (!Uri.TryCreate(origins.ToString(), UriKind.Absolute, out var origin) || origin.Authority != context.Request.Host.Value))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return;
        }
        await next(context);
    }
}
