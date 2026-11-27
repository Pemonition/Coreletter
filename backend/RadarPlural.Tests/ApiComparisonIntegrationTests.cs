using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Npgsql;
using RadarPlural.Api;

namespace RadarPlural.Tests;

[Collection("postgres")]
public class ApiComparisonIntegrationTests(PostgresFixture pg)
{
    private async Task ResetAsync() => await pg.ExecAsync("""
        DELETE FROM "StoryAudits";
        DELETE FROM "UsageReservations";
        DELETE FROM "UsageMonths";
        DELETE FROM "FeedItems";
        DELETE FROM "Stories";
        DELETE FROM "Members";
        """);

    private async Task<Guid> SeedCorroboratedStoryAsync(string slug)
    {
        var storyId = await pg.SeedStoryAsync();
        var now = DateTimeOffset.UtcNow;
        for (var i = 1; i <= 2; i++)
        {
            await pg.ExecAsync("""
                INSERT INTO "FeedItems" ("Id", "SourceId", "SourceName", "Topic", "Url", "Title", "Summary", "PublishedAt", "CollectedAt", "Status", "Flags", "StoryId")
                VALUES ($1, $2, $3, 'tecnologia', $4, $5, $6, $7, $7, 'triado', '', $8)
                """,
                Guid.NewGuid(),
                $"fonte-{slug}-{i}",
                $"Fonte {i}",
                $"https://fonte{i}.example.invalid/{slug}/{Guid.NewGuid():N}",
                $"Aurora apresenta projeto {slug} com {i * 100} testes",
                $"Resumo fictício do projeto {slug} com {i * 100} testes.",
                now.AddMinutes(-i),
                storyId);
        }
        return storyId;
    }

    private string ApiConnectionString()
    {
        var baseCs = DatabaseUrl.ToNpgsql(Environment.GetEnvironmentVariable("INTEGRATION_DATABASE_URL")!);
        using var conn = pg.Db.CreateConnection();
        return new NpgsqlConnectionStringBuilder(baseCs) { Database = conn.Database }.ConnectionString;
    }

    private sealed class ApiServer : IAsyncDisposable
    {
        private readonly Process process;
        public string BaseUrl { get; }

        private ApiServer(Process process, string baseUrl)
        {
            this.process = process;
            BaseUrl = baseUrl;
        }

        public static async Task<ApiServer> StartAsync(string connectionString, string? bootstrapAdminEmail = null)
        {
            using var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();

            var apiDll = Path.Combine(AppContext.BaseDirectory, "RadarPlural.Api.dll");
            var psi = new ProcessStartInfo("dotnet", $"exec \"{apiDll}\"")
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };
            psi.Environment["PORT"] = port.ToString();
            psi.Environment["ASPNETCORE_ENVIRONMENT"] = "Development";
            psi.Environment["ConnectionStrings__Default"] = connectionString;
            psi.Environment["Database__MigrateOnStartup"] = "false";
            if (!string.IsNullOrWhiteSpace(bootstrapAdminEmail))
                psi.Environment["BootstrapAdminEmail"] = bootstrapAdminEmail;

            var process = Process.Start(psi) ?? throw new InvalidOperationException("Não foi possível iniciar RadarPlural.Api.");
            var baseUrl = $"http://127.0.0.1:{port}";
            var server = new ApiServer(process, baseUrl);

            using var probe = new HttpClient { BaseAddress = new Uri(baseUrl) };
            var deadline = DateTime.UtcNow.AddSeconds(15);
            while (DateTime.UtcNow < deadline)
            {
                if (process.HasExited)
                {
                    var stderr = await process.StandardError.ReadToEndAsync();
                    throw new InvalidOperationException($"API encerrou prematuramente ({process.ExitCode}): {stderr}");
                }
                try
                {
                    var health = await probe.GetAsync("/api/health");
                    if (health.IsSuccessStatusCode) return server;
                }
                catch
                {
                    // API ainda subindo.
                }
                await Task.Delay(100);
            }

            await server.DisposeAsync();
            throw new TimeoutException("API não respondeu em /api/health no tempo esperado.");
        }

        public HttpClient CreateClient() => new(new HttpClientHandler { CookieContainer = new CookieContainer() })
        {
            BaseAddress = new Uri(BaseUrl)
        };

        public async ValueTask DisposeAsync()
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync();
            }
            process.Dispose();
        }
    }

    [IntegrationFact]
    public async Task ReusedIdempotencyKeyAcrossDifferentStoriesReturns409ProblemDetails()
    {
        await ResetAsync();
        var storyA = await SeedCorroboratedStoryAsync("a");
        var storyB = await SeedCorroboratedStoryAsync("b");

        await using var api = await ApiServer.StartAsync(ApiConnectionString());
        using var client = api.CreateClient();

        var register = await client.PostAsJsonAsync("/api/auth/register", new
        {
            email = $"d1-{Guid.NewGuid():N}@example.invalid",
            password = "SenhaDeTeste123!",
            displayName = "Teste D1"
        });
        Assert.Equal(HttpStatusCode.OK, register.StatusCode);

        var key = Guid.NewGuid().ToString();
        using var firstReq = new HttpRequestMessage(HttpMethod.Post, "/api/comparisons")
        {
            Content = JsonContent.Create(new { storyId = storyA })
        };
        firstReq.Headers.Add("Idempotency-Key", key);
        var first = await client.SendAsync(firstReq);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        using var secondReq = new HttpRequestMessage(HttpMethod.Post, "/api/comparisons")
        {
            Content = JsonContent.Create(new { storyId = storyB })
        };
        secondReq.Headers.Add("Idempotency-Key", key);
        var second = await client.SendAsync(secondReq);

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        Assert.Equal("application/problem+json", second.Content.Headers.ContentType?.MediaType);
        using var body = JsonDocument.Parse(await second.Content.ReadAsStringAsync());
        Assert.Equal(409, body.RootElement.GetProperty("status").GetInt32());
        Assert.Equal("chave_reutilizada", body.RootElement.GetProperty("code").GetString());
    }
}
