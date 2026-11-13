namespace RadarPlural.Application.Common;

// Formato de página do contrato: {items,page,pageSize,total}.
public sealed record PageDto<T>(IReadOnlyList<T> Items, int Page, int PageSize, int Total);
