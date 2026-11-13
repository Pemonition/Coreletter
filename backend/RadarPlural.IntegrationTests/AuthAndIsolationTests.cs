using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RadarPlural.Infrastructure.Persistence;

namespace RadarPlural.IntegrationTests;

// Autenticação, autorização e isolamento entre contas, contra PostgreSQL real.
// Complementa scripts/Test-Api.ps1: aqui roda no "dotnet test", com falha apontando o cenário exato.
[Collection(ApiCollection.Name)]
public sealed class AuthAndIsolationTests(ApiFixture fx)
{
    [Fact]
    public async Task MigrationsCoverTheModel()
    {
        // Pega a migração esquecida: alguém muda uma entidade e não gera o arquivo em Migrations.
        using var scope = fx.Api.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDb>();
        Assert.Empty(await db.Database.GetPendingMigrationsAsync());
        Assert.False(db.Database.HasPendingModelChanges());
    }

    [Fact]
    public async Task AnonymousHasNoIdentity()
    {
        var response = await fx.Api.Browser().GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task RegisterStartsSessionAndLogoutEndsIt()
    {
        var (client, user) = await fx.Api.SignedUpAsync();
        var me = await client.GetFromJsonAsync<UserView>("/api/auth/me");
        Assert.Equal(user.Id, me!.Id);
        Assert.False(me.IsAdmin);

        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsync("/api/auth/logout", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/auth/me")).StatusCode);
    }

    [Fact]
    public async Task LoginAcceptsRightPasswordAndRejectsWrongOne()
    {
        var (_, user) = await fx.Api.SignedUpAsync();
        var browser = fx.Api.Browser();
        var wrong = await browser.PostAsJsonAsync("/api/auth/login", new { email = user.Email, password = "senha-errada-000" });
        Assert.Equal(HttpStatusCode.Unauthorized, wrong.StatusCode);
        var right = await browser.PostAsJsonAsync("/api/auth/login", new { email = user.Email.ToUpperInvariant(), password = TestData.Password });
        Assert.Equal(HttpStatusCode.OK, right.StatusCode);
    }

    [Fact]
    public async Task DuplicateEmailIsRejectedWithoutRevealingIt()
    {
        var (_, user) = await fx.Api.SignedUpAsync();
        var again = await fx.Api.Browser().PostAsJsonAsync("/api/auth/register", new { email = user.Email, password = TestData.Password, displayName = "Outro" });
        Assert.Equal(HttpStatusCode.BadRequest, again.StatusCode);
        Assert.Contains("Não foi possível cadastrar", await ErrorOf(again));
    }

    [Theory]
    [InlineData("nao-e-email", "senha-de-teste-123", "Nome")]
    [InlineData("a@example.invalid", "curta", "Nome")]
    [InlineData("a@example.invalid", "senha-de-teste-123", "")]
    public async Task InvalidRegistrationReturns400WithErrorField(string email, string password, string displayName)
    {
        var response = await fx.Api.Browser().PostAsJsonAsync("/api/auth/register", new { email, password, displayName });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.False(string.IsNullOrWhiteSpace(await ErrorOf(response)));
    }

    [Fact]
    public async Task OnlyTheAuthorDeletesAndDeletionRemovesDependents()
    {
        var (author, _) = await fx.Api.SignedUpAsync("Autor");
        var (other, _) = await fx.Api.SignedUpAsync("Terceiro");
        var post = await CreatePostAsync(author);

        Assert.Equal(HttpStatusCode.NoContent, (await other.PutAsync($"/api/posts/{post.Id}/save", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await other.PostAsJsonAsync($"/api/posts/{post.Id}/reports", new { reason = "Denúncia fictícia" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await other.DeleteAsync($"/api/posts/{post.Id}")).StatusCode);

        Assert.Equal(HttpStatusCode.NoContent, (await author.DeleteAsync($"/api/posts/{post.Id}")).StatusCode);
        var saved = await other.GetFromJsonAsync<PageOf<PostView>>("/api/posts/saved");
        Assert.DoesNotContain(saved!.Items, x => x.Id == post.Id);
        Assert.Equal(HttpStatusCode.NotFound, (await author.DeleteAsync($"/api/posts/{post.Id}")).StatusCode);
    }

    [Fact]
    public async Task SavedItemsArePrivateAndSavingIsIdempotent()
    {
        var (a, _) = await fx.Api.SignedUpAsync("A");
        var (b, _) = await fx.Api.SignedUpAsync("B");
        var post = await CreatePostAsync(a);

        Assert.Equal(HttpStatusCode.NoContent, (await b.PutAsync($"/api/posts/{post.Id}/save", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await b.PutAsync($"/api/posts/{post.Id}/save", null)).StatusCode);

        var savedB = await b.GetFromJsonAsync<PageOf<PostView>>("/api/posts/saved");
        Assert.Single(savedB!.Items, x => x.Id == post.Id);
        var savedA = await a.GetFromJsonAsync<PageOf<PostView>>("/api/posts/saved");
        Assert.DoesNotContain(savedA!.Items, x => x.Id == post.Id);
    }

    [Fact]
    public async Task ProtectedEndpointsRequireSession()
    {
        var anonymous = fx.Api.Browser();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PostAsJsonAsync("/api/posts", TestData.Post())).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/posts/saved")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/moderation/reports")).StatusCode);
    }

    [Fact]
    public async Task ReaderCannotModerate()
    {
        var (reader, _) = await fx.Api.SignedUpAsync();
        Assert.Equal(HttpStatusCode.Forbidden, (await reader.GetAsync("/api/moderation/reports")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await reader.PutAsync($"/api/moderation/posts/{Guid.NewGuid()}/hide", null)).StatusCode);
    }

    [Fact]
    public async Task CrossOriginMutationIsBlockedButSameOriginWorks()
    {
        var (client, _) = await fx.Api.SignedUpAsync();
        using var foreign = new HttpRequestMessage(HttpMethod.Post, "/api/posts") { Content = JsonContent.Create(TestData.Post()) };
        foreign.Headers.Add("Origin", "https://site-malicioso.example");
        Assert.Equal(HttpStatusCode.Forbidden, (await client.SendAsync(foreign)).StatusCode);

        using var same = new HttpRequestMessage(HttpMethod.Post, "/api/posts") { Content = JsonContent.Create(TestData.Post()) };
        same.Headers.Add("Origin", "http://localhost");
        Assert.Equal(HttpStatusCode.Created, (await client.SendAsync(same)).StatusCode);
    }

    [Fact]
    public async Task FeedHidesSavedFlagFromOtherAccounts()
    {
        var (a, _) = await fx.Api.SignedUpAsync("A");
        var (b, _) = await fx.Api.SignedUpAsync("B");
        var post = await CreatePostAsync(a);
        await b.PutAsync($"/api/posts/{post.Id}/save", null);

        Assert.True(await IsSavedInFeed(b, post.Id));
        Assert.False(await IsSavedInFeed(a, post.Id));
        Assert.False(await IsSavedInFeed(fx.Api.Browser(), post.Id));
    }

    private static async Task<bool> IsSavedInFeed(HttpClient client, Guid id)
    {
        var feed = await client.GetFromJsonAsync<PageOf<PostView>>("/api/posts?pageSize=50");
        return feed!.Items.Single(x => x.Id == id).IsSaved;
    }

    private static async Task<PostView> CreatePostAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync("/api/posts", TestData.Post());
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<PostView>())!;
    }

    // Contrato de erro: { "error": "..." } (o front lê esse campo).
    private static async Task<string> ErrorOf(HttpResponseMessage response)
    {
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return json.RootElement.GetProperty("error").GetString() ?? "";
    }
}
