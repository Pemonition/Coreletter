using RadarPlural.Domain.Posts;

namespace RadarPlural.Application.Posts;

public sealed record PostInput(string? Url, string? Title, string? Topic, string? Type, string? Comment);
public sealed record ReportInput(string? Reason);

public sealed record PostDto(Guid Id, string Url, string Title, string Topic, string Type, string Comment, Guid AuthorId, string AuthorName, DateTimeOffset CreatedAt, bool IsSaved)
{
    public static PostDto From(Post p, bool isSaved) => new(p.Id, p.Url, p.Title, p.Topic, p.Type, p.Comment, p.AuthorId, p.AuthorName, p.CreatedAt, isSaved);
}
