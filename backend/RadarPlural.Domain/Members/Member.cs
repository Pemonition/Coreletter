namespace RadarPlural.Domain.Members;

public class Member
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Email { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public bool IsAdmin { get; set; }

    public const int MaxEmailLength = 254;
    public const int MaxDisplayNameLength = 80;
    public const int MinPasswordLength = 10;
    public const int MaxPasswordLength = 128;

    public static string NormalizeEmail(string? email) => email?.Trim().ToLowerInvariant() ?? "";
}
