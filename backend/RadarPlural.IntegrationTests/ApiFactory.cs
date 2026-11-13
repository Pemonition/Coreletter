using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace RadarPlural.IntegrationTests;

// Sobe a API real (Program.cs) em memória, apontando para o PostgreSQL do fixture.
public sealed class ApiFactory(PostgresFixture db) : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseSetting("ConnectionStrings:Default", db.ConnectionString);
        builder.UseSetting("RateLimit:PermitLimit", "100000");
    }

    // Cada cliente guarda o próprio cookie: equivale a um navegador diferente.
    public HttpClient Browser() => CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true });

    public async Task<(HttpClient Client, UserView User)> SignedUpAsync(string name = "Leitor")
    {
        var client = Browser();
        var email = $"{name.ToLowerInvariant()}-{Guid.NewGuid():N}@example.invalid";
        var response = await client.PostAsJsonAsync("/api/auth/register", new { email, password = TestData.Password, displayName = name });
        response.EnsureSuccessStatusCode();
        return (client, (await response.Content.ReadFromJsonAsync<UserView>())!);
    }
}

public static class TestData
{
    public const string Password = "senha-de-teste-123";

    public static object Post(string suffix = "") => new
    {
        url = $"https://example.org/{Guid.NewGuid():N}{suffix}",
        title = "Publicação de teste",
        topic = "IA",
        type = "article",
        comment = "Conteúdo fictício"
    };
}

public sealed record UserView(Guid Id, string Email, string DisplayName, bool IsAdmin);
public sealed record PostView(Guid Id, string Url, string Title, Guid AuthorId, bool IsSaved);
public sealed record PageOf<T>(List<T> Items, int Page, int PageSize, int Total);
