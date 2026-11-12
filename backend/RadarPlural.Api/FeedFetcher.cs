using System.Net;
using System.Net.Sockets;

namespace RadarPlural.Api;

public interface IFeedFetcher
{
    Task<IReadOnlyList<FeedEntry>> Fetch(FeedSource source, CancellationToken cancellationToken);
}

public sealed class FeedFetcher(HttpClient client) : IFeedFetcher
{
    public static SocketsHttpHandler Handler() => new()
    {
        AllowAutoRedirect = false, UseProxy = false, UseCookies = false,
        AutomaticDecompression = DecompressionMethods.None, ConnectTimeout = TimeSpan.FromSeconds(5),
        ConnectCallback = async (ctx, ct) =>
        {
            if (ctx.DnsEndPoint.Port != 443) throw new HttpRequestException("Porta não permitida.");
            var addresses = await Dns.GetHostAddressesAsync(ctx.DnsEndPoint.Host, ct);
            if (addresses.Length == 0 || addresses.Any(a => !CollectionRules.PublicIp(a)))
                throw new HttpRequestException("Destino de rede não permitido.");
            // Pin the validated address in the actual connection: no second DNS resolution.
            foreach (var address in addresses)
            {
                var socket = new Socket(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
                try
                {
                    await socket.ConnectAsync(new IPEndPoint(address, 443), ct);
                    return new NetworkStream(socket, ownsSocket: true);
                }
                catch (SocketException) { socket.Dispose(); }
                catch { socket.Dispose(); throw; }
            }
            throw new HttpRequestException("Nenhum endereço público da fonte respondeu.");
        }
    };

    public async Task<IReadOnlyList<FeedEntry>> Fetch(FeedSource source, CancellationToken cancellationToken)
    {
        if (!source.Approved || !CollectionRules.ValidSourceUrl(source.Url))
            throw new HttpRequestException("Fonte não habilitada.");
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(12));
        using var request = new HttpRequestMessage(HttpMethod.Get, source.Url);
        request.Headers.UserAgent.ParseAdd("CoreletterCourse/0.2");
        request.Headers.Accept.ParseAdd("application/rss+xml, application/atom+xml, application/xml, text/xml");
        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
        if ((int)response.StatusCode is >= 300 and < 400) throw new HttpRequestException("Redirecionamento não permitido; revise a fonte.");
        response.EnsureSuccessStatusCode();
        if (response.Content.Headers.ContentEncoding.Count != 0) throw new HttpRequestException("Feed comprimido não aceito.");
        var media = response.Content.Headers.ContentType?.MediaType;
        if (media is not ("application/rss+xml" or "application/atom+xml" or "application/xml" or "text/xml"))
            throw new HttpRequestException("Fonte deve responder RSS/Atom XML.");
        if (response.Content.Headers.ContentLength > 524288) throw new HttpRequestException("Feed excede 512 KiB.");
        await using var stream = await response.Content.ReadAsStreamAsync(timeout.Token);
        using var buffer = new MemoryStream();
        var bytes = new byte[8192];
        int read;
        while ((read = await stream.ReadAsync(bytes, timeout.Token)) > 0)
        {
            if (buffer.Length + read > 524288) throw new HttpRequestException("Feed excede 512 KiB.");
            buffer.Write(bytes, 0, read);
        }
        buffer.Position = 0;
        return CollectionRules.Parse(buffer);
    }
}
