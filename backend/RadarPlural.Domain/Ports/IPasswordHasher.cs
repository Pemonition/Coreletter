using RadarPlural.Domain.Members;

namespace RadarPlural.Domain.Ports;

// O domínio não sabe qual algoritmo é usado; o adapter atual usa o PasswordHasher do ASP.NET Core Identity (PBKDF2 com sal).
public interface IPasswordHasher
{
    string Hash(Member member, string password);
    bool Verify(Member member, string password);
}
