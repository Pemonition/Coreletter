namespace RadarPlural.Domain.Common;

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int Total);

public static class Paging
{
    public const int MaxPage = 1_000_000;
    public const int MaxPageSize = 50;

    public static bool IsValidPage(int page) => page is >= 1 and <= MaxPage;
    public static int ClampPageSize(int pageSize) => Math.Clamp(pageSize, 1, MaxPageSize);
}
