using Microsoft.EntityFrameworkCore;
using RadarPlural.Domain.Ports;
using RadarPlural.Domain.Posts;

namespace RadarPlural.Infrastructure.Persistence.Repositories;

public sealed class ReportRepository(AppDb db) : IReportRepository
{
    public async Task AddAsync(Report report, CancellationToken ct)
    {
        db.Reports.Add(report);
        await db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<Report>> ListRecentAsync(int limit, CancellationToken ct) =>
        await db.Reports.AsNoTracking().OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id).Take(limit).ToListAsync(ct);
}
