using System.Net;
using System.Net.Sockets;

namespace RadarPlural.Ingestion;

// Download com as travas da política: lista de hosts, redirecionamentos conferidos um a um,
// tempo limite total e teto de bytes medido no corpo já descompactado.
public sealed class SafeFeedFetcher(HttpClient http, IngestionPolicy policy)
{
    public async Task<byte[]> FetchAsync(Uri url, IReadOnlySet<string> allowedHosts, CancellationToken ct = default)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(policy.Timeout);
        var current = url;
        try
        {
            for (var hop = 0; ; hop++)
            {
                if (!UrlGuard.IsAllowedTarget(current, allowedHosts))
                    throw new IngestionException($"Destino fora da política de ingestão: {current.Scheme}://{current.Host}");

                using var request = new HttpRequestMessage(HttpMethod.Get, current);
                request.Headers.UserAgent.ParseAdd(policy.UserAgent);
                request.Headers.Accept.ParseAdd("application/rss+xml, application/atom+xml, application/xml;q=0.9, text/xml;q=0.8");
                using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);

                if ((int)response.StatusCode is >= 300 and < 400 && response.Headers.Location is { } location)
                {
                    if (hop >= policy.MaxRedirects) throw new IngestionException($"Mais de {policy.MaxRedirects} redirecionamentos.");
                    current = Resolve(current, location);
                    continue;
                }
                if (!response.IsSuccessStatusCode) throw new IngestionException($"Fonte respondeu HTTP {(int)response.StatusCode}.");
                if (response.Content.Headers.ContentLength > policy.MaxBytes) throw new IngestionException($"Feed maior que {policy.MaxBytes} bytes.");

                await using var body = await response.Content.ReadAsStreamAsync(timeout.Token);
                return await ReadLimitedAsync(body, timeout.Token);
            }
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new IngestionException($"Tempo limite de {policy.Timeout.TotalSeconds:0}s excedido.");
        }
        catch (HttpRequestException e)
        {
            throw new IngestionException($"Falha de rede: {e.Message}", e);
        }
    }

    // Em Linux, um Location "/novo" vira file:///novo; caminho iniciado por "/" é relativo ao site atual.
    private static Uri Resolve(Uri current, Uri location) =>
        location.IsAbsoluteUri && !(location.IsFile && location.OriginalString.StartsWith('/'))
            ? location
            : new Uri(current, location.OriginalString);

    private async Task<byte[]> ReadLimitedAsync(Stream body, CancellationToken ct)
    {
        using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        int read;
        while ((read = await body.ReadAsync(chunk, ct)) > 0)
        {
            if (buffer.Length + read > policy.MaxBytes) throw new IngestionException($"Feed maior que {policy.MaxBytes} bytes.");
            buffer.Write(chunk, 0, read);
        }
        return buffer.ToArray();
    }
}

public static class SafeHttp
{
    // Cliente real do coletor. A checagem de IP acontece na conexão, depois do DNS: um host da lista
    // que resolva para rede interna é recusado. Sem proxy de propósito: com proxy, o IP checado seria o do proxy.
    public static HttpClient CreateClient(IngestionPolicy policy)
    {
        var handler = new SocketsHttpHandler
        {
            AllowAutoRedirect = false,
            UseProxy = false,
            UseCookies = false,
            AutomaticDecompression = DecompressionMethods.All,
            ConnectTimeout = policy.Timeout,
            MaxResponseHeadersLength = 64,
            PooledConnectionLifetime = TimeSpan.FromMinutes(2),
            ConnectCallback = async (context, ct) =>
            {
                var addresses = await Dns.GetHostAddressesAsync(context.DnsEndPoint.Host, ct);
                if (addresses.Length == 0 || !addresses.All(UrlGuard.IsPublicAddress))
                    throw new HttpRequestException($"{context.DnsEndPoint.Host} resolve para endereço não público; bloqueado pela política de ingestão.");
                var socket = new Socket(SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
                try
                {
                    await socket.ConnectAsync(addresses, context.DnsEndPoint.Port, ct);
                    return new NetworkStream(socket, ownsSocket: true);
                }
                catch
                {
                    socket.Dispose();
                    throw;
                }
            }
        };
        return new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan }; // o limite é aplicado no fetcher
    }
}
