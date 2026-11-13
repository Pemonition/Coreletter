using RadarPlural.Application.Common;
using RadarPlural.Application.Moderation;
using RadarPlural.Application.Posts;
using RadarPlural.Domain.Members;

namespace RadarPlural.Tests.Application;

public class PostServiceTests
{
    private readonly InMemoryMembers members = new();
    private readonly InMemoryPosts posts = new();
    private readonly InMemoryReports reports = new();
    private readonly Member author = new() { DisplayName = "Autora", Email = "autora@example.org" };
    private readonly Member reader = new() { DisplayName = "Leitor", Email = "leitor@example.org" };

    public PostServiceTests() => members.Items.AddRange([author, reader]);

    private PostService Service() => new(posts, reports, members);
    private static PostInput Valid(string type = "article") => new("https://example.org/a", " Título ", " IA ", type, null);

    private async Task<PostDto> PublishAsync()
    {
        var created = await Service().CreateAsync(Valid(), author.Id, default);
        return created.Value!;
    }

    [Theory]
    [InlineData("article")]
    [InlineData("podcast")]
    [InlineData("music")]
    public async Task AllowedTypesArePublishedWithAuthorFromSession(string type)
    {
        var result = await Service().CreateAsync(Valid(type), author.Id, default);

        Assert.True(result.IsSuccess);
        Assert.Equal(author.Id, result.Value!.AuthorId);
        Assert.Equal("Autora", result.Value.AuthorName);
        Assert.Equal("Título", result.Value.Title);
    }

    [Theory]
    [InlineData("javascript:alert(1)", "Título", "IA", "article")]
    [InlineData("https://example.org", "", "IA", "article")]
    [InlineData("https://example.org", "Título", "", "article")]
    [InlineData("https://example.org", "Título", "IA", "video")]
    public async Task InvalidPostIsRejectedAndNotStored(string url, string title, string topic, string type)
    {
        var result = await Service().CreateAsync(new(url, title, topic, type, null), author.Id, default);

        Assert.Equal(ErrorKind.Validation, result.Error?.Kind);
        Assert.Empty(posts.Items);
    }

    [Fact]
    public async Task TitleAndCommentLimitsAreInclusive()
    {
        Assert.True((await Service().CreateAsync(new("https://e.org", new string('t', 180), "IA", "article", new string('c', 2000)), author.Id, default)).IsSuccess);
        Assert.False((await Service().CreateAsync(new("https://e.org", new string('t', 181), "IA", "article", null), author.Id, default)).IsSuccess);
        Assert.False((await Service().CreateAsync(new("https://e.org", "T", "IA", "article", new string('c', 2001)), author.Id, default)).IsSuccess);
    }

    [Fact]
    public async Task OnlyAuthorDeletes()
    {
        var post = await PublishAsync();

        Assert.Equal(ErrorKind.Forbidden, (await Service().DeleteAsync(post.Id, reader.Id, default)).Error?.Kind);
        Assert.Single(posts.Items);
        Assert.True((await Service().DeleteAsync(post.Id, author.Id, default)).IsSuccess);
        Assert.Equal(ErrorKind.NotFound, (await Service().DeleteAsync(post.Id, author.Id, default)).Error?.Kind);
    }

    [Fact]
    public async Task HiddenPostCannotBeSavedOrReportedAndLeavesSavedList()
    {
        var post = await PublishAsync();
        await Service().SaveAsync(post.Id, reader.Id, default);

        await new ModerationService(reports, posts).HideAsync(post.Id, default);

        Assert.Equal(ErrorKind.NotFound, (await Service().SaveAsync(post.Id, reader.Id, default)).Error?.Kind);
        Assert.Equal(ErrorKind.NotFound, (await Service().ReportAsync(post.Id, new("spam"), reader.Id, default)).Error?.Kind);
        Assert.Empty((await Service().SavedAsync(reader.Id, default)).Items);
        Assert.Equal(0, (await Service().FeedAsync(1, 20, null, null, null, default)).Value!.Total);
    }

    [Fact]
    public async Task SavedFlagBelongsToTheViewerOnly()
    {
        var post = await PublishAsync();
        await Service().SaveAsync(post.Id, reader.Id, default);

        Assert.True((await Service().FeedAsync(1, 20, null, null, reader.Id, default)).Value!.Items.Single().IsSaved);
        Assert.False((await Service().FeedAsync(1, 20, null, null, author.Id, default)).Value!.Items.Single().IsSaved);
        Assert.False((await Service().FeedAsync(1, 20, null, null, null, default)).Value!.Items.Single().IsSaved);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1_000_001)]
    public async Task PageOutsideLimitsIsRejected(int page) =>
        Assert.Equal(ErrorKind.Validation, (await Service().FeedAsync(page, 20, null, null, null, default)).Error?.Kind);

    [Theory]
    [InlineData(0, 1)]
    [InlineData(500, 50)]
    public async Task PageSizeIsClamped(int requested, int expected) =>
        Assert.Equal(expected, (await Service().FeedAsync(1, requested, null, null, null, default)).Value!.PageSize);

    [Fact]
    public async Task ReportNeedsReasonWithinLimit()
    {
        var post = await PublishAsync();
        Assert.Equal(ErrorKind.Validation, (await Service().ReportAsync(post.Id, new("  "), reader.Id, default)).Error?.Kind);
        Assert.Equal(ErrorKind.Validation, (await Service().ReportAsync(post.Id, new(new string('r', 1001)), reader.Id, default)).Error?.Kind);
        Assert.True((await Service().ReportAsync(post.Id, new(" spam "), reader.Id, default)).IsSuccess);
        Assert.Equal("spam", reports.Items.Single().Reason);
    }
}
