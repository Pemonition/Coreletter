using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace RadarPlural.IntegrationTests;

// Comportamento transversal da API: formato de erro, saúde, cabeçalhos e documentação.
[Collection(ApiCollection.Name)]
public sealed class HttpQualityTests(ApiFixture fx)
{
    [Fact]
    public async Task ValidationErrorIsProblemDetailsAndKeepsErrorField()
    {
        var response = await fx.Api.Browser().PostAsJsonAsync("/api/auth/register", new { email = "x", password = "y", displayName = "" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(400, json.RootElement.GetProperty("status").GetInt32());
        Assert.False(string.IsNullOrWhiteSpace(json.RootElement.GetProperty("error").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(json.RootElement.GetProperty("traceId").GetString()));
    }

    [Fact]
    public async Task UnauthenticatedResponseAlsoHasErrorBody()
    {
        var response = await fx.Api.Browser().GetAsync("/api/posts/saved");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("Entre na sua conta para continuar.", json.RootElement.GetProperty("error").GetString());
    }

    [Fact]
    public async Task ReadinessChecksTheDatabase()
    {
        var response = await fx.Api.Browser().GetAsync("/api/health/ready");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task SecurityHeadersArePresent()
    {
        var response = await fx.Api.Browser().GetAsync("/api/health");
        Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Equal("DENY", response.Headers.GetValues("X-Frame-Options").Single());
        Assert.Contains("frame-ancestors 'none'", response.Headers.GetValues("Content-Security-Policy").Single());
    }

    [Fact]
    public async Task OpenApiDocumentDescribesTheEndpoints()
    {
        using var json = JsonDocument.Parse(await fx.Api.Browser().GetStringAsync("/openapi/v1.json"));
        var paths = json.RootElement.GetProperty("paths");
        foreach (var path in new[] { "/api/auth/register", "/api/posts", "/api/posts/{id}", "/api/news", "/api/moderation/reports" })
            Assert.True(paths.TryGetProperty(path, out _), $"OpenAPI sem {path}");
    }

    [Fact]
    public async Task UnknownPostIsNotFound()
    {
        var (client, _) = await fx.Api.SignedUpAsync();
        var response = await client.PutAsync($"/api/posts/{Guid.NewGuid()}/save", null);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
