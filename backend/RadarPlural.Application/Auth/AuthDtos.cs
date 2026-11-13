using RadarPlural.Domain.Members;

namespace RadarPlural.Application.Auth;

public sealed record RegisterInput(string? Email, string? Password, string? DisplayName);
public sealed record LoginInput(string? Email, string? Password);
public sealed record UserDto(Guid Id, string Email, string DisplayName, bool IsAdmin)
{
    public static UserDto From(Member m) => new(m.Id, m.Email, m.DisplayName, m.IsAdmin);
}
