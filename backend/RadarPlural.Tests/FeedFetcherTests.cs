using System.Net;
using System.Text;
using RadarPlural.Api;

public class FeedFetcherTests
{
    static readonly FeedSource Source = new("test", "Teste", "https://news.example.com/feed", "pt", true);
    [Fact]
    public async Task UnapprovedSourceNeverMakesRequest()
    {
        var handler = new Stub(_ => throw new Exception("Não deve acessar rede"));
        using var client = new HttpClient(handler);
        await Assert.ThrowsAsync<HttpRequestException>(() => new FeedFetcher(client).Fetch(Source with { Approved = false }, default));
        Assert.Equal(0, handler.Calls);
    }
    [Theory]
    [InlineData(302, "application/rss+xml")]
    [InlineData(200, "text/html")]
    [InlineData(429, "application/rss+xml")]
    public async Task InvalidHttpResponseIsFailure(int status, string contentType)
    {
        using var client = new HttpClient(new Stub(_ => new HttpResponseMessage((HttpStatusCode)status)
        { Content = new StringContent("<rss><channel/></rss>", Encoding.UTF8, contentType) }));
        await Assert.ThrowsAsync<HttpRequestException>(() => new FeedFetcher(client).Fetch(Source, default));
    }
    [Fact]
    public async Task DownloadLimitAppliesWithoutContentLength()
    {
        using var client = new HttpClient(new Stub(_ =>
        {
            var content = new StreamContent(new MemoryStream(new byte[524289]));
            content.Headers.ContentType = new("application/rss+xml");
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = content };
        }));
        await Assert.ThrowsAsync<HttpRequestException>(() => new FeedFetcher(client).Fetch(Source, default));
    }
    [Fact]
    public async Task ReadsRssWithConfiguredSourceOnly()
    {
        using var client = new HttpClient(new Stub(request =>
        {
            Assert.Equal(Source.Url, request.RequestUri!.AbsoluteUri);
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(
                "<rss><channel><item><title>C#</title><link>https://news.example.com/a</link></item></channel></rss>", Encoding.UTF8, "application/rss+xml") };
        }));
        Assert.Single(await new FeedFetcher(client).Fetch(Source, default));
    }
    sealed class Stub(Func<HttpRequestMessage, HttpResponseMessage> reply) : HttpMessageHandler
    {
        public int Calls { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        { Calls++; return Task.FromResult(reply(request)); }
    }
}
