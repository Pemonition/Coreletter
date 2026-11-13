using RadarPlural.Domain.Members;

namespace RadarPlural.Domain.Ports;

// Porta de saída: o domínio diz O QUE precisa; o adapter (EF Core/PostgreSQL) decide COMO.
public interface IMemberRepository
{
    Task<Member?> FindByIdAsync(Guid id, CancellationToken ct);
    Task<Member?> FindByEmailAsync(string normalizedEmail, CancellationToken ct);
    Task<bool> EmailExistsAsync(string normalizedEmail, CancellationToken ct);

    // false quando o e-mail já existe (inclusive em corrida entre dois cadastros simultâneos).
    Task<bool> TryAddAsync(Member member, CancellationToken ct);
}
