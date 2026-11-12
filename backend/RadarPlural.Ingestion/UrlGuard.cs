using System.Net;
using System.Net.Sockets;

namespace RadarPlural.Ingestion;

// Proteção contra SSRF em duas camadas:
// 1) IsAllowedTarget: só hosts da lista aprovada, http/https, porta padrão, sem credenciais na URL.
// 2) IsPublicAddress: usado no momento da conexão (SafeHttp), depois da resolução DNS. Barra um host
//    da lista que passe a apontar para rede interna (DNS rebinding, metadados de nuvem etc.).
public static class UrlGuard
{
    public static bool IsAllowedTarget(Uri uri, IReadOnlySet<string> allowedHosts) =>
        uri.IsAbsoluteUri
        && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp)
        && string.IsNullOrEmpty(uri.UserInfo)
        && uri.IsDefaultPort
        && allowedHosts.Contains(uri.IdnHost);

    public static bool IsPublicAddress(IPAddress address)
    {
        if (address.IsIPv4MappedToIPv6) address = address.MapToIPv4();
        return address.AddressFamily switch
        {
            AddressFamily.InterNetwork => IsPublicV4(address.GetAddressBytes()),
            AddressFamily.InterNetworkV6 => IsPublicV6(address),
            _ => false
        };
    }

    private static bool IsPublicV4(byte[] b) => !(
        b[0] == 0                                      // 0.0.0.0/8 "esta rede"
        || b[0] == 10                                  // 10/8 privada
        || b[0] == 100 && (b[1] & 0xC0) == 64          // 100.64/10 CGNAT
        || b[0] == 127                                 // loopback
        || b[0] == 169 && b[1] == 254                  // link-local e metadados de nuvem
        || b[0] == 172 && (b[1] & 0xF0) == 16          // 172.16/12 privada
        || b[0] == 192 && b[1] == 0 && b[2] is 0 or 2  // 192.0.0/24 IETF, 192.0.2/24 documentação
        || b[0] == 192 && b[1] == 168                  // 192.168/16 privada
        || b[0] == 198 && (b[1] & 0xFE) == 18          // 198.18/15 benchmark
        || b[0] == 198 && b[1] == 51 && b[2] == 100    // documentação
        || b[0] == 203 && b[1] == 0 && b[2] == 113     // documentação
        || b[0] >= 224);                               // multicast, reservado e broadcast

    private static bool IsPublicV6(IPAddress a)
    {
        if (IPAddress.IPv6Loopback.Equals(a) || IPAddress.IPv6None.Equals(a)) return false;
        if (a.IsIPv6LinkLocal || a.IsIPv6SiteLocal || a.IsIPv6Multicast || a.IsIPv6UniqueLocal) return false;
        var b = a.GetAddressBytes();
        if (b.Take(12).All(x => x == 0)) return false;                       // ::/96 compatível com IPv4
        if (b[0] == 0x00 && b[1] == 0x64 && b[2] == 0xff && b[3] == 0x9b) return false; // 64:ff9b::/96 NAT64
        if (b[0] == 0x20 && b[1] == 0x01 && b[2] == 0x0d && b[3] == 0xb8) return false; // 2001:db8::/32 documentação
        if (b[0] == 0x20 && b[1] == 0x02) return false;                      // 2002::/16 6to4 pode embutir IPv4 interno
        return true;
    }
}
