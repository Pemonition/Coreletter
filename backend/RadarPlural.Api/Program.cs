using System.Diagnostics;
using Microsoft.AspNetCore.Authentication.Cookies;
using RadarPlural.Api.Endpoints;
using RadarPlural.Api.Http;
using RadarPlural.Application;
using RadarPlural.Infrastructure;
using RadarPlural.Infrastructure.Persistence;

// Program.cs só compõe a aplicação (arquitetura hexagonal):
//   Domain         entidades, regras e portas (interfaces)
//   Application    casos de uso
//   Infrastructure adapters de saída (EF Core/PostgreSQL, hash de senha)
//   Api            adapter de entrada (HTTP): este projeto
var builder = WebApplication.CreateBuilder(args);

// Plataformas de hospedagem (Render, Kubernetes) informam a porta em PORT; localmente a API continua em localhost:5080.
var port = Environment.GetEnvironmentVariable("PORT");
builder.WebHost.UseUrls(string.IsNullOrWhiteSpace(port) ? "http://localhost:5080" : $"http://0.0.0.0:{port}");
builder.WebHost.ConfigureKestrel(o => o.Limits.MaxRequestBodySize = 32768);

// Em produção, log em JSON (uma linha por evento): é o formato que Kubernetes, Render e coletores de log leem.
if (!builder.Environment.IsDevelopment())
{
    builder.Logging.ClearProviders();
    builder.Logging.AddJsonConsole(o =>
    {
        o.IncludeScopes = true;
        o.UseUtcTimestamp = true;
        o.TimestampFormat = "yyyy-MM-ddTHH:mm:ss.fffZ";
    });
}

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

// Todo erro sai como ProblemDetails (RFC 9457) com "error" (contrato do front) e "traceId" (para achar o log).
builder.Services.AddProblemDetails(o => o.CustomizeProblemDetails = ctx =>
{
    var problem = ctx.ProblemDetails;
    problem.Extensions.TryAdd("error", problem.Detail ?? ProblemMessages.For(problem.Status));
    problem.Extensions.TryAdd("traceId", Activity.Current?.Id ?? ctx.HttpContext.TraceIdentifier);
});
builder.Services.AddOpenApi();

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie(o =>
{
    o.Cookie.Name = "radarplural.session";
    o.Cookie.HttpOnly = true;
    o.Cookie.SameSite = SameSiteMode.Strict;
    o.Cookie.SecurePolicy = builder.Environment.IsDevelopment() ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
    o.ExpireTimeSpan = TimeSpan.FromHours(8);
    // API não redireciona para página de login: devolve o status e o front decide.
    o.Events.OnRedirectToLogin = c => { c.Response.StatusCode = StatusCodes.Status401Unauthorized; return Task.CompletedTask; };
    o.Events.OnRedirectToAccessDenied = c => { c.Response.StatusCode = StatusCodes.Status403Forbidden; return Task.CompletedTask; };
});
builder.Services.AddAuthorization();
builder.Services.AddApiRateLimiting(builder.Configuration);

var app = builder.Build();

// "dotnet RadarPlural.Api.dll --migrate": aplica as migrações e encerra.
// No Kubernetes isso roda num initContainer, antes das réplicas subirem, em vez de cada réplica migrar ao iniciar.
if (args.Contains("--migrate"))
{
    await DatabaseInitializer.InitializeAsync(app.Services, app.Configuration);
    return;
}
if (app.Configuration.GetValue("Database:MigrateOnStartup", true))
    await DatabaseInitializer.InitializeAsync(app.Services, app.Configuration);

app.UseExceptionHandler(); // 500 sem stack trace para o cliente; detalhe vai para o log
app.UseStatusCodePages();  // 401/403/404/429 sem corpo também viram ProblemDetails
if (!app.Environment.IsDevelopment()) app.UseHsts();
app.UseMiddleware<SecurityHeaders>();
app.UseMiddleware<OriginGuard>();

// Em produção o build Angular é copiado para wwwroot: front e API na mesma origem, sem CORS.
app.UseDefaultFiles();
app.UseStaticFiles();

app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

// Documentação OpenAPI: /openapi/v1.json e interface em /swagger. Ligada em desenvolvimento ou com OpenApi__Enabled=true.
if (app.Environment.IsDevelopment() || app.Configuration.GetValue("OpenApi:Enabled", false))
{
    app.MapOpenApi();
    app.UseSwaggerUI(o =>
    {
        o.SwaggerEndpoint("/openapi/v1.json", "Coreletter API v1");
        o.DocumentTitle = "Coreletter API";
    });
}

app.MapGroup("/api")
    .MapHealth()
    .MapAuth()
    .MapPosts()
    .MapNews()
    .MapModeration();

app.Run();

// Exposto para os testes de integração (WebApplicationFactory<Program>).
public partial class Program;
