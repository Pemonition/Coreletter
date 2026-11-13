using RadarPlural.Domain.Posts;

namespace RadarPlural.Domain.Ports;

public interface IReportRepository
{
    Task AddAsync(Report report, CancellationToken ct);
    Task<IReadOnlyList<Report>> ListRecentAsync(int limit, CancellationToken ct);
}
