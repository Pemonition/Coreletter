namespace RadarPlural.Domain.Posts;

public static class LinkRules
{
    // Só http(s) absoluto e sem credenciais embutidas (bloqueia javascript:, file:, user:senha@host).
    public static bool ValidUrl(string? value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp)
        && !string.IsNullOrWhiteSpace(uri.Host)
        && string.IsNullOrEmpty(uri.UserInfo);
}
