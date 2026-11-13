namespace RadarPlural.Domain.Posts;

// Link compartilhado por um membro. A API guarda só o link e o texto informado; não baixa o conteúdo.
public class Post
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Url { get; set; } = "";
    public string Title { get; set; } = "";
    public string Topic { get; set; } = "";
    public string Type { get; set; } = "";
    public string Comment { get; set; } = "";
    public Guid AuthorId { get; set; }
    public string AuthorName { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public bool Hidden { get; set; }

    public const int MaxUrlLength = 2048;
    public const int MaxTitleLength = 180;
    public const int MaxTopicLength = 60;
    public const int MaxCommentLength = 2000;

    public bool IsAuthoredBy(Guid memberId) => AuthorId == memberId;
}

public static class PostTypes
{
    public const string Article = "article";
    public const string Podcast = "podcast";
    public const string Music = "music";

    public static bool IsValid(string? type) => type is Article or Podcast or Music;
}
