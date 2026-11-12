using System.Net;
using System.Text;
using RadarPlural.Api;

public class CollectionTests
{
    static readonly FeedSource Source = new("test", "Fonte de teste", "https://news.example.com/feed.xml", "pt", true);
    static CollectionInput Input => new("Tecnologia", ["automação"], [], ["test"], 6, 10, true);
    [Fact]
    public void QuestionnaireRequiresApprovedCatalogAndBoundedSchedule()
    {
        Assert.Null(CollectionRules.Validate(Input, [Source]));
        Assert.NotNull(CollectionRules.Validate(Input, [Source with { Approved = false }]));
        Assert.NotNull(CollectionRules.Validate(Input with { SourceIds = ["https://evil.example/feed"] }, [Source]));
        Assert.NotNull(CollectionRules.Validate(Input with { SourceIds = ["test", "test"] }, [Source]));
        Assert.NotNull(CollectionRules.Validate(Input with { IntervalHours = 1 }, [Source]));
        Assert.NotNull(CollectionRules.Validate(Input with { MaxItems = 21 }, [Source]));
        Assert.NotNull(CollectionRules.Validate(Input with { Keywords = [] }, [Source]));
        Assert.NotNull(CollectionRules.Validate(Input with { Keywords = [null!] }, [Source]));
    }
    [Theory]
    [InlineData("127.0.0.1")]
    [InlineData("10.1.2.3")]
    [InlineData("169.254.169.254")]
    [InlineData("100.64.0.1")]
    [InlineData("172.16.0.1")]
    [InlineData("192.168.1.1")]
    [InlineData("198.18.0.1")]
    [InlineData("::1")]
    [InlineData("::ffff:127.0.0.1")]
    [InlineData("fc00::1")]
    [InlineData("fe80::1")]
    [InlineData("2001:db8::1")]
    [InlineData("2002:7f00:1::1")]
    [InlineData("2001::1")]
    public void NonPublicAddressesAreRejected(string address) => Assert.False(CollectionRules.PublicIp(IPAddress.Parse(address)));
    [Theory]
    [InlineData("1.1.1.1")]
    [InlineData("8.8.8.8")]
    [InlineData("2606:4700:4700::1111")]
    public void PublicAddressesAccepted(string address) => Assert.True(CollectionRules.PublicIp(IPAddress.Parse(address)));
    [Theory]
    [InlineData("http://news.example.com/feed")]
    [InlineData("https://localhost/feed")]
    [InlineData("https://127.0.0.1/feed")]
    [InlineData("https://user:pass@news.example.com/feed")]
    [InlineData("https://news.example.com:8443/feed")]
    public void FetchUrlRequiresHttpsDnsAndDefaultPort(string url) => Assert.False(CollectionRules.ValidSourceUrl(url));
    [Fact]
    public void KeywordMatchIgnoresAccentsAndHonorsExclusions()
    {
        var entry = new FeedEntry("https://news.example.com/a", "AUTOMACAO industrial", "Estudo", null);
        Assert.True(CollectionRules.Matches(entry, Input));
        Assert.False(CollectionRules.Matches(entry, Input with { ExcludedKeywords = ["industrial"] }));
        Assert.False(CollectionRules.Matches(entry with { Title = "Outro assunto" }, Input));
    }
    [Fact]
    public void RssParsesPlainTextDeduplicatesAndRejectsUnsafeLinks()
    {
        using var stream = Xml("""
            <rss><channel>
              <item><title>Automação</title><link>https://news.example.com/a</link><description><![CDATA[<b>Teste</b> &amp; dados]]></description></item>
              <item><title>Duplicado</title><link>https://news.example.com/a</link></item>
              <item><title>Inválido</title><link>javascript:alert(1)</link></item>
            </channel></rss>
            """);
        var entry = Assert.Single(CollectionRules.Parse(stream));
        Assert.Equal("Teste & dados", entry.Summary);
        Assert.Equal("https://news.example.com/a", entry.Url);
    }
    [Fact]
    public void AtomSelectsAlternateLinkAndKeepsSourceDate()
    {
        using var stream = Xml("""
            <feed xmlns="http://www.w3.org/2005/Atom"><entry><title>IA</title>
            <link rel="self" href="https://news.example.com/api"/><link rel="alternate" href="https://news.example.com/a"/>
            <summary>Texto</summary><published>2026-10-27T12:00:00Z</published></entry></feed>
            """);
        var entry = Assert.Single(CollectionRules.Parse(stream));
        Assert.Equal("https://news.example.com/a", entry.Url);
        Assert.Equal(2026, entry.PublishedAt?.Year);
    }
    [Fact]
    public void XmlExternalEntitiesAreRejected()
    {
        using var stream = Xml("<!DOCTYPE rss [<!ENTITY x SYSTEM 'file:///secret'>]><rss><channel><item><title>&x;</title></item></channel></rss>");
        Assert.Throws<System.Xml.XmlException>(() => CollectionRules.Parse(stream));
    }
    [Fact]
    public void HtmlIsNotSilentlyTreatedAsEmptySuccessfulFeed()
    {
        using var stream = Xml("<html><body>login</body></html>");
        Assert.Throws<System.Xml.XmlException>(() => CollectionRules.Parse(stream));
    }
    static MemoryStream Xml(string text) => new(Encoding.UTF8.GetBytes(text));
}
