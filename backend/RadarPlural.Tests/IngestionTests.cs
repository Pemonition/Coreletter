using System.Net;
using System.Text;
using RadarPlural.Ingestion;

namespace RadarPlural.Tests;

// Política de ingestão (docs/decisoes/0002-ingestao.md): SSRF, limites e higienização do conteúdo externo.
public class UrlGuardTests
{
    [Theory]
    [InlineData("127.0.0.1")]
    [InlineData("10.1.2.3")]
    [InlineData("172.16.0.1")]
    [InlineData("192.168.0.10")]
    [InlineData("169.254.169.254")] // metadados de nuvem
    [InlineData("100.64.0.1")]      // CGNAT
    [InlineData("0.0.0.0")]
    [InlineData("224.0.0.1")]
    [InlineData("255.255.255.255")]
    [InlineData("::1")]
    [InlineData("fe80::1")]
    [InlineData("fd00::1")]
    [InlineData("::ffff:127.0.0.1")] // IPv4 mapeado em IPv6
    [InlineData("::")]
    public void PrivateOrSpecialAddressesAreBlocked(string ip) => Assert.False(UrlGuard.IsPublicAddress(IPAddress.Parse(ip)));

    [Theory]
    [InlineData("8.8.8.8")]
    [InlineData("200.147.3.157")]
    [InlineData("2001:4860:4860::8888")]
    public void PublicAddressesAreAllowed(string ip) => Assert.True(UrlGuard.IsPublicAddress(IPAddress.Parse(ip)));

    private static readonly HashSet<string> Allowed = new(StringComparer.OrdinalIgnoreCase) { "tecnoblog.net" };

    [Theory]
    [InlineData("https://tecnoblog.net/feed/", true)]
    [InlineData("https://TECNOBLOG.net/feed/", true)]
    [InlineData("http://tecnoblog.net/feed/", true)]
    [InlineData("https://evil.example/feed", false)]
    [InlineData("https://tecnoblog.net.evil.example/", false)]
    [InlineData("https://sub.tecnoblog.net/", false)]
    [InlineData("https://user:pw@tecnoblog.net/", false)]
    [InlineData("https://tecnoblog.net:8443/", false)]
    [InlineData("ftp://tecnoblog.net/", false)]
    [InlineData("file:///etc/passwd", false)]
    public void OnlyAllowlistedHostsOnDefaultPortsAreFetched(string url, bool expected) =>
        Assert.Equal(expected, UrlGuard.IsAllowedTarget(new Uri(url), Allowed));
}

public class UrlNormalizerTests
{
    [Theory]
    [InlineData("https://Example.org/a?utm_source=x&id=7&fbclid=y#topo", "https://example.org/a?id=7")]
    [InlineData("HTTPS://EXAMPLE.ORG:443/A", "https://example.org/A")]
    [InlineData("https://example.org/a?utm_medium=rss", "https://example.org/a")]
    [InlineData("  https://example.org/a  ", "https://example.org/a")]
    public void TrackingAndFragmentsAreRemoved(string raw, string expected) => Assert.Equal(expected, UrlNormalizer.Normalize(raw));

    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("data:text/html,oi")]
    [InlineData("https://user:pw@example.org/")]
    [InlineData("")]
    [InlineData(null)]
    public void UnsafeOrEmptyUrlsAreRejected(string? raw) => Assert.Null(UrlNormalizer.Normalize(raw));

    [Fact]
    public void RelativeLinksResolveAgainstFeed() =>
        Assert.Equal("https://example.org/noticia/1", UrlNormalizer.Normalize("/noticia/1", new Uri("https://example.org/feed.xml")));
}

public class FeedParserTests
{
    private static readonly Uri FeedUrl = new("https://example.org/feed");
    private static readonly IngestionPolicy Policy = new();

    private static IReadOnlyList<FeedEntry> Parse(string xml, IngestionPolicy? policy = null, Encoding? encoding = null) =>
        FeedParser.Parse(new MemoryStream((encoding ?? Encoding.UTF8).GetBytes(xml)), FeedUrl, policy ?? Policy);

    [Fact]
    public void ReadsRss2WithHtmlStrippedFromSummary()
    {
        var items = Parse("""
            <?xml version="1.0" encoding="UTF-8"?>
            <rss version="2.0"><channel><title>T</title>
              <item>
                <title>Nova &amp;amp; lei de IA</title>
                <link>https://example.org/ia?utm_source=rss</link>
                <description><![CDATA[<p>Texto <b>importante</b> &amp; <script>alert(1)</script>fim</p>]]></description>
                <pubDate>Thu, 12 Nov 2026 10:30:00 -0300</pubDate>
              </item>
            </channel></rss>
            """);
        var item = Assert.Single(items);
        Assert.Equal("Nova & lei de IA", item.Title);
        Assert.Equal("https://example.org/ia", item.Url);
        Assert.Equal("Texto importante & fim", item.Summary);
        Assert.Equal(new DateTimeOffset(2026, 11, 12, 10, 30, 0, TimeSpan.FromHours(-3)), item.PublishedAt);
    }

    [Fact]
    public void ReadsAtomPreferringAlternateLink()
    {
        var items = Parse("""
            <feed xmlns="http://www.w3.org/2005/Atom">
              <entry>
                <title type="html">Chip &lt;b&gt;novo&lt;/b&gt;</title>
                <link rel="self" href="https://example.org/self"/>
                <link rel="alternate" href="https://example.org/chip"/>
                <summary>Resumo</summary>
                <updated>2026-11-12T13:00:00Z</updated>
              </entry>
            </feed>
            """);
        var item = Assert.Single(items);
        Assert.Equal("Chip novo", item.Title);
        Assert.Equal("https://example.org/chip", item.Url);
        Assert.Equal(new DateTimeOffset(2026, 11, 12, 13, 0, 0, TimeSpan.Zero), item.PublishedAt);
    }

    [Fact]
    public void ReadsLegacyLatin1Rss091()
    {
        var items = Parse("""
            <?xml version="1.0" encoding="ISO-8859-1" ?>
            <rss version="0.91"><channel><item><title>Ação no mercado</title><link>https://example.org/acao</link></item></channel></rss>
            """, encoding: Encoding.Latin1);
        Assert.Equal("Ação no mercado", Assert.Single(items).Title);
    }

    [Fact]
    public void DtdAndExternalEntitiesAreRejected()
    {
        var xxe = """
            <?xml version="1.0"?>
            <!DOCTYPE rss [<!ENTITY xxe SYSTEM "file:///etc/passwd">]>
            <rss version="2.0"><channel><item><title>&xxe;</title><link>https://example.org/x</link></item></channel></rss>
            """;
        Assert.Throws<IngestionException>(() => Parse(xxe));
    }

    [Fact]
    public void MalformedXmlIsAnIngestionError() => Assert.Throws<IngestionException>(() => Parse("<rss><channel><item>"));

    [Fact]
    public void UnknownRootIsAnIngestionError() => Assert.Throws<IngestionException>(() => Parse("<html><body>oi</body></html>"));

    [Fact]
    public void ItemsWithoutTitleOrSafeLinkAreSkippedAndDuplicatesCollapse()
    {
        var items = Parse("""
            <rss version="2.0"><channel>
              <item><title>Sem link</title></item>
              <item><title>Script</title><link>javascript:alert(1)</link></item>
              <item><link>https://example.org/sem-titulo</link></item>
              <item><title>A</title><link>https://example.org/a?utm_source=x</link></item>
              <item><title>A de novo</title><link>https://example.org/a</link></item>
              <item><title>Guid permalink</title><guid>https://example.org/g</guid></item>
              <item><title>Guid opaco</title><guid isPermaLink="false">https://example.org/opaco</guid></item>
            </channel></rss>
            """);
        Assert.Equal(["https://example.org/a", "https://example.org/g"], items.Select(x => x.Url));
    }

    [Fact]
    public void LimitsFromPolicyAreApplied()
    {
        var policy = new IngestionPolicy { MaxEntriesPerFeed = 2, MaxTitleLength = 10, MaxSummaryLength = 12 };
        var xml = "<rss version=\"2.0\"><channel>" + string.Concat(Enumerable.Range(1, 5).Select(i =>
            $"<item><title>Titulo muito comprido {i}</title><link>https://example.org/{i}</link><description>palavra palavra palavra</description></item>")) + "</channel></rss>";
        var items = Parse(xml, policy);
        Assert.Equal(2, items.Count);
        Assert.All(items, x => Assert.True(x.Title.Length <= 10));
        Assert.All(items, x => Assert.True(x.Summary.Length <= 12));
        Assert.EndsWith("…", items[0].Summary);
    }

    [Theory]
    [InlineData("Thu, 12 Nov 2026 13:30:00 GMT")]
    [InlineData("Thu, 12 Nov 2026 13:30:00 +0000")]
    [InlineData("12 Nov 2026 10:30:00 -0300")]
    [InlineData("Thu, 12 Nov 2026 10:30:00 BRT")]
    [InlineData("2026-11-12T13:30:00Z")]
    public void CommonFeedDateFormatsAreUnderstood(string date)
    {
        var items = Parse($"<rss version=\"2.0\"><channel><item><title>A</title><link>https://example.org/a</link><pubDate>{date}</pubDate></item></channel></rss>");
        Assert.Equal(new DateTimeOffset(2026, 11, 12, 13, 30, 0, TimeSpan.Zero), Assert.Single(items).PublishedAt?.ToUniversalTime());
    }

    [Fact]
    public void UnparseableDateBecomesNull()
    {
        var items = Parse("<rss version=\"2.0\"><channel><item><title>A</title><link>https://example.org/a</link><pubDate>ontem</pubDate></item></channel></rss>");
        Assert.Null(Assert.Single(items).PublishedAt);
    }
}

public class SafeFeedFetcherTests
{
    private static readonly HashSet<string> Allowed = new(StringComparer.OrdinalIgnoreCase) { "feeds.example.org", "cdn.example.org" };

    private sealed class FakeHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public List<Uri> Requests { get; } = [];
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Requests.Add(request.RequestUri!);
            return Task.FromResult(respond(request));
        }
    }

    private static HttpResponseMessage Ok(string body) => new(HttpStatusCode.OK) { Content = new StringContent(body) };

    private static HttpResponseMessage Redirect(string location) => new(HttpStatusCode.MovedPermanently) { Headers = { Location = new Uri(location, UriKind.RelativeOrAbsolute) } };

    [Fact]
    public async Task DownloadsAllowlistedFeed()
    {
        var handler = new FakeHandler(_ => Ok("<rss/>"));
        var fetcher = new SafeFeedFetcher(new HttpClient(handler), new IngestionPolicy());
        var bytes = await fetcher.FetchAsync(new Uri("https://feeds.example.org/rss"), Allowed);
        Assert.Equal("<rss/>", Encoding.UTF8.GetString(bytes));
    }

    [Fact]
    public async Task NonAllowlistedStartIsBlockedWithoutAnyRequest()
    {
        var handler = new FakeHandler(_ => Ok("<rss/>"));
        var fetcher = new SafeFeedFetcher(new HttpClient(handler), new IngestionPolicy());
        await Assert.ThrowsAsync<IngestionException>(() => fetcher.FetchAsync(new Uri("https://evil.example/rss"), Allowed));
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task RedirectToAllowlistedHostIsFollowed()
    {
        var handler = new FakeHandler(r => r.RequestUri!.Host == "feeds.example.org" ? Redirect("https://cdn.example.org/rss") : Ok("<rss/>"));
        var fetcher = new SafeFeedFetcher(new HttpClient(handler), new IngestionPolicy());
        await fetcher.FetchAsync(new Uri("https://feeds.example.org/rss"), Allowed);
        Assert.Equal(["https://feeds.example.org/rss", "https://cdn.example.org/rss"], handler.Requests.Select(x => x.ToString()));
    }

    [Fact]
    public async Task RelativeRedirectStaysOnHost()
    {
        var handler = new FakeHandler(r => r.RequestUri!.AbsolutePath == "/old" ? Redirect("/new") : Ok("<rss/>"));
        var fetcher = new SafeFeedFetcher(new HttpClient(handler), new IngestionPolicy());
        await fetcher.FetchAsync(new Uri("https://feeds.example.org/old"), Allowed);
        Assert.Equal("https://feeds.example.org/new", handler.Requests[^1].ToString());
    }

    [Theory]
    [InlineData("http://169.254.169.254/latest/meta-data/")]
    [InlineData("https://evil.example/rss")]
    [InlineData("file:///etc/passwd")]
    public async Task RedirectOutsideAllowlistIsBlocked(string location)
    {
        var handler = new FakeHandler(r => r.RequestUri!.Host == "feeds.example.org" ? Redirect(location) : Ok("segredo"));
        var fetcher = new SafeFeedFetcher(new HttpClient(handler), new IngestionPolicy());
        await Assert.ThrowsAsync<IngestionException>(() => fetcher.FetchAsync(new Uri("https://feeds.example.org/rss"), Allowed));
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task RedirectLoopStopsAtLimit()
    {
        var handler = new FakeHandler(r => Redirect("https://feeds.example.org/loop"));
        var fetcher = new SafeFeedFetcher(new HttpClient(handler), new IngestionPolicy { MaxRedirects = 3 });
        await Assert.ThrowsAsync<IngestionException>(() => fetcher.FetchAsync(new Uri("https://feeds.example.org/rss"), Allowed));
        Assert.Equal(4, handler.Requests.Count);
    }

    [Fact]
    public async Task BodyLargerThanLimitIsRejectedEvenWithoutContentLength()
    {
        var big = new string('x', 2048);
        var handler = new FakeHandler(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(new MemoryStream(Encoding.UTF8.GetBytes(big))) });
        var fetcher = new SafeFeedFetcher(new HttpClient(handler), new IngestionPolicy { MaxBytes = 1024 });
        await Assert.ThrowsAsync<IngestionException>(() => fetcher.FetchAsync(new Uri("https://feeds.example.org/rss"), Allowed));
    }

    [Fact]
    public async Task DeclaredContentLengthAboveLimitIsRejected()
    {
        var handler = new FakeHandler(_ => Ok(new string('x', 2048)));
        var fetcher = new SafeFeedFetcher(new HttpClient(handler), new IngestionPolicy { MaxBytes = 1024 });
        await Assert.ThrowsAsync<IngestionException>(() => fetcher.FetchAsync(new Uri("https://feeds.example.org/rss"), Allowed));
    }

    [Fact]
    public async Task HttpErrorIsAnIngestionError()
    {
        var handler = new FakeHandler(_ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
        var fetcher = new SafeFeedFetcher(new HttpClient(handler), new IngestionPolicy());
        await Assert.ThrowsAsync<IngestionException>(() => fetcher.FetchAsync(new Uri("https://feeds.example.org/rss"), Allowed));
    }
}

public class SourceCatalogTests
{
    [Fact]
    public void ValidCatalogLoadsAndExposesHostAllowlist()
    {
        var sources = SourceCatalog.Parse("""
            [{ "id": "tecnoblog", "name": "Tecnoblog", "url": "https://tecnoblog.net/feed/", "topic": "tecnologia" },
             { "id": "g1-tec", "name": "g1 Tecnologia", "url": "https://g1.globo.com/rss/g1/tecnologia/", "topic": "tecnologia" }]
            """);
        Assert.Equal(2, sources.Count);
        Assert.Equal(["g1.globo.com", "tecnoblog.net"], SourceCatalog.AllowedHosts(sources).Order());
    }

    [Theory]
    [InlineData("""[{ "id": "a", "name": "A", "url": "http://example.org/feed", "topic": "t" }]""")]          // sem https
    [InlineData("""[{ "id": "a", "name": "A", "url": "https://127.0.0.1/feed", "topic": "t" }]""")]          // IP literal
    [InlineData("""[{ "id": "a", "name": "A", "url": "https://user:x@example.org/", "topic": "t" }]""")]     // credencial
    [InlineData("""[{ "id": "A B", "name": "A", "url": "https://example.org/feed", "topic": "t" }]""")]      // id inválido
    [InlineData("""[{ "id": "a", "name": "", "url": "https://example.org/feed", "topic": "t" }]""")]         // nome vazio
    [InlineData("""[{ "id": "a", "name": "A", "url": "https://example.org/1", "topic": "t" }, { "id": "a", "name": "B", "url": "https://example.org/2", "topic": "t" }]""")] // id repetido
    [InlineData("[]")]
    [InlineData("{")]
    public void InvalidCatalogIsRejected(string json) => Assert.Throws<IngestionException>(() => SourceCatalog.Parse(json));
}

public class FeedDatesTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 12, 15, 0, 0, TimeSpan.Zero);

    [Fact]
    public void PastDateIsKept() => Assert.Equal(Now.AddHours(-5), FeedDates.PublishedOrNull(Now.AddHours(-5), Now));

    [Fact]
    public void SmallClockSkewIsTolerated() => Assert.NotNull(FeedDates.PublishedOrNull(Now.AddMinutes(30), Now));

    [Fact]
    public void FutureDateIsDiscardedSoItCannotPinTheFeed() => Assert.Null(FeedDates.PublishedOrNull(Now.AddDays(2), Now));
}

public class IngestionHardeningTests
{
    [Fact]
    public void InlineTagsDoNotLeaveSpaceBeforePunctuation() =>
        Assert.Equal("O Chip, lançado (hoje). Fim!", FeedParser.Clean("<p>O <a href='x'>Chip</a>, lançado (<b>hoje</b>). Fim<i>!</i></p>", 100));

    [Fact]
    public void DefaultSummaryIsShortSnippetNotFullArticle() => Assert.True(new IngestionPolicy().MaxSummaryLength <= 300);

    [Fact]
    public async Task AllowlistedHostResolvingToLoopbackIsBlockedAtConnection()
    {
        var policy = new IngestionPolicy { Timeout = TimeSpan.FromSeconds(5) };
        using var http = SafeHttp.CreateClient(policy);
        var fetcher = new SafeFeedFetcher(http, policy);
        var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "localhost" };
        var error = await Assert.ThrowsAsync<IngestionException>(() => fetcher.FetchAsync(new Uri("http://localhost/feed"), allowed));
        Assert.Contains("não público", error.Message);
    }
}
