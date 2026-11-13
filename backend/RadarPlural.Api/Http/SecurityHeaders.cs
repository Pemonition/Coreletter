namespace RadarPlural.Api.Http;

// Cabeçalhos de segurança básicos para a API e para o Angular servido pela própria API.
public sealed class SecurityHeaders(RequestDelegate next)
{
    public Task InvokeAsync(HttpContext context)
    {
        var headers = context.Response.Headers;
        headers.XContentTypeOptions = "nosniff";
        headers.XFrameOptions = "DENY";
        headers["Referrer-Policy"] = "no-referrer";
        headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";
        // CSP só fora do Swagger (a UI dele usa script inline). 'unsafe-inline' em style: o Angular injeta estilos de componente.
        if (!context.Request.Path.StartsWithSegments("/swagger"))
            headers.ContentSecurityPolicy = "default-src 'self'; img-src 'self' data: https:; style-src 'self' 'unsafe-inline'; frame-ancestors 'none'; base-uri 'self'; form-action 'self'";
        return next(context);
    }
}
