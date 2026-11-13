using Microsoft.AspNetCore.Diagnostics.HealthChecks;

namespace RadarPlural.Api.Endpoints;

public static class HealthEndpoints
{
    public static RouteGroupBuilder MapHealth(this RouteGroupBuilder api)
    {
        // Vivacidade (livenessProbe): o processo responde. NÃO consulta o banco de propósito:
        // se o banco cair, reiniciar a API não resolve nada e só gera reinícios em cascata.
        api.MapGet("/health", () => Results.Ok(new { status = "ok" })).WithTags("Saúde").ExcludeFromDescription();

        // Prontidão (readinessProbe): só recebe tráfego quem alcança o banco.
        api.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = c => c.Tags.Contains("ready") });
        return api;
    }
}
