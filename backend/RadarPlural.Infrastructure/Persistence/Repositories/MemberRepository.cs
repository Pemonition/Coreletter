using Microsoft.EntityFrameworkCore;
using Npgsql;
using RadarPlural.Domain.Members;
using RadarPlural.Domain.Ports;

namespace RadarPlural.Infrastructure.Persistence.Repositories;

public sealed class MemberRepository(AppDb db) : IMemberRepository
{
    public async Task<Member?> FindByIdAsync(Guid id, CancellationToken ct) => await db.Members.FindAsync([id], ct);

    public Task<Member?> FindByEmailAsync(string normalizedEmail, CancellationToken ct) =>
        db.Members.SingleOrDefaultAsync(x => x.Email == normalizedEmail, ct);

    public Task<bool> EmailExistsAsync(string normalizedEmail, CancellationToken ct) =>
        db.Members.AnyAsync(x => x.Email == normalizedEmail, ct);

    public async Task<bool> TryAddAsync(Member member, CancellationToken ct)
    {
        db.Members.Add(member);
        try
        {
            await db.SaveChangesAsync(ct);
            return true;
        }
        catch (DbUpdateException e) when (e.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            // Dois cadastros simultâneos com o mesmo e-mail: o índice único decide, sem 500.
            db.Entry(member).State = EntityState.Detached;
            return false;
        }
    }
}
