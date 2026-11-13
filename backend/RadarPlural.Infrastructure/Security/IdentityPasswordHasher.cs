using Microsoft.AspNetCore.Identity;
using RadarPlural.Domain.Members;

namespace RadarPlural.Infrastructure.Security;

// Adapter da porta IPasswordHasher sobre o PasswordHasher do ASP.NET Core Identity (PBKDF2, sal por senha).
// Os hashes já gravados continuam válidos: é o mesmo algoritmo usado antes da reestruturação.
public sealed class IdentityPasswordHasher : Domain.Ports.IPasswordHasher
{
    private readonly PasswordHasher<Member> inner = new();

    public string Hash(Member member, string password) => inner.HashPassword(member, password);

    public bool Verify(Member member, string password) =>
        inner.VerifyHashedPassword(member, member.PasswordHash, password) != PasswordVerificationResult.Failed;
}
