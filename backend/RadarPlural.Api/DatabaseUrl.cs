using Npgsql;

namespace RadarPlural.Api;

// Provedores gerenciados entregam postgresql://usuario:senha@host:porta/banco; o Npgsql espera chave=valor.
public static class DatabaseUrl
{
    public static string ToNpgsql(string value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || uri.Scheme is not ("postgres" or "postgresql")) return value;
        var credentials = uri.UserInfo.Split(':', 2);
        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = uri.Host,
            Port = uri.IsDefaultPort || uri.Port < 0 ? 5432 : uri.Port,
            Database = Uri.UnescapeDataString(uri.AbsolutePath.TrimStart('/')),
            Username = Uri.UnescapeDataString(credentials[0]),
            Password = credentials.Length > 1 ? Uri.UnescapeDataString(credentials[1]) : null
        };
        foreach (var pair in uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = pair.Split('=', 2);
            if (parts.Length == 2 && parts[0].Equals("sslmode", StringComparison.OrdinalIgnoreCase) && Enum.TryParse<SslMode>(parts[1], true, out var sslMode)) builder.SslMode = sslMode;
        }
        return builder.ConnectionString;
    }
}
