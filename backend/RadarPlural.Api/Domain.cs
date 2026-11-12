using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace RadarPlural.Api;


public sealed class AppDb(DbContextOptions<AppDb> options) : DbContext(options), IDataProtectionKeyContext
{
    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();
    public DbSet<Member> Members => Set<Member>();
    public DbSet<Post> Posts => Set<Post>();
    public DbSet<SavedPost> Saved => Set<SavedPost>();
    public DbSet<Report> Reports => Set<Report>();
    public DbSet<FeedItem> FeedItems => Set<FeedItem>();
    public DbSet<Story> Stories => Set<Story>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Member>().HasIndex(x => x.Email).IsUnique();
        b.Entity<SavedPost>().HasKey(x => new { x.MemberId, x.PostId });
        b.Entity<SavedPost>().HasOne<Post>().WithMany().HasForeignKey(x => x.PostId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<Report>().HasOne<Post>().WithMany().HasForeignKey(x => x.PostId).OnDelete(DeleteBehavior.Cascade);

        // Itens coletados pelo RadarPlural.Worker. A URL canônica é a chave de deduplicação (ON CONFLICT DO NOTHING).
        b.Entity<FeedItem>(e =>
        {
            e.HasIndex(x => x.Url).IsUnique();
            e.HasIndex(x => x.SourceId);
            e.Property(x => x.SourceId).HasMaxLength(64);
            e.Property(x => x.SourceName).HasMaxLength(120);
            e.Property(x => x.Topic).HasMaxLength(60);
            e.Property(x => x.Url).HasMaxLength(2048);
            e.Property(x => x.Title).HasMaxLength(300);
            e.Property(x => x.Summary).HasMaxLength(1000);
            // Triagem (decisoes/0003): "triado" segue no pipeline; "descartado" guarda o motivo e sai do feed.
            e.Property(x => x.Status).HasMaxLength(20).HasDefaultValue("triado");
            e.Property(x => x.DiscardReason).HasMaxLength(40);
            e.Property(x => x.Flags).HasMaxLength(200).HasDefaultValue("");
            e.HasIndex(x => new { x.Status, x.CollectedAt });
            e.HasOne<Story>().WithMany().HasForeignKey(x => x.StoryId).OnDelete(DeleteBehavior.SetNull);
        });
        // História = notícias de veículos diferentes sobre o mesmo acontecimento. Estatísticas recalculadas pelo Worker.
        b.Entity<Story>(e =>
        {
            e.Property(x => x.Title).HasMaxLength(300);
            e.Property(x => x.Topic).HasMaxLength(60);
            e.Property(x => x.Status).HasMaxLength(20).HasDefaultValue("agrupada");
            e.HasIndex(x => x.LastPublishedAt);
        });
    }
}
public class Member
{
    public Guid Id
    {
        get;
        set;
    } = Guid.NewGuid();
    public string Email
    {
        get;
        set;
    } = "";
    public string DisplayName
    {
        get;
        set;
    } = "";
    public string PasswordHash
    {
        get;
        set;
    } = "";
    public bool IsAdmin
    {
        get;
        set;
    }
}
public class Post
{
    public Guid Id
    {
        get;
        set;
    } = Guid.NewGuid();
    public string Url
    {
        get;
        set;
    } = "";
    public string Title
    {
        get;
        set;
    } = "";
    public string Topic
    {
        get;
        set;
    } = "";
    public string Type
    {
        get;
        set;
    } = "";
    public string Comment
    {
        get;
        set;
    } = "";
    public Guid AuthorId
    {
        get;
        set;
    }
    public string AuthorName
    {
        get;
        set;
    } = "";
    public DateTimeOffset CreatedAt
    {
        get;
        set;
    } = DateTimeOffset.UtcNow;
    public bool Hidden
    {
        get;
        set;
    }
}
public class SavedPost
{
    public Guid MemberId
    {
        get;
        set;
    }
    public Guid PostId
    {
        get;
        set;
    }
}
public class Report
{
    public Guid Id
    {
        get;
        set;
    } = Guid.NewGuid();
    public Guid MemberId
    {
        get;
        set;
    }
    public Guid PostId
    {
        get;
        set;
    }
    public string Reason
    {
        get;
        set;
    } = "";
    public DateTimeOffset CreatedAt
    {
        get;
        set;
    } = DateTimeOffset.UtcNow;
}
// Notícia coletada de fonte aprovada. Guarda só título, resumo curto e link: o texto completo fica no site de origem.
public class FeedItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string SourceId { get; set; } = "";
    public string SourceName { get; set; } = "";
    public string Topic { get; set; } = "";
    public string Url { get; set; } = "";
    public string Title { get; set; } = "";
    public string Summary { get; set; } = "";
    public DateTimeOffset? PublishedAt { get; set; }
    public DateTimeOffset CollectedAt { get; set; } = DateTimeOffset.UtcNow;
    public string Status { get; set; } = "triado";
    public string? DiscardReason { get; set; }
    public string Flags { get; set; } = "";
    public Guid? StoryId { get; set; }
}
public class Story
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = "";
    public string Topic { get; set; } = "";
    public string Status { get; set; } = "agrupada";
    public int ItemCount { get; set; }
    public int SourceCount { get; set; }
    public DateTimeOffset FirstPublishedAt { get; set; }
    public DateTimeOffset LastPublishedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
public record RegisterInput(string Email, string Password, string DisplayName);

public record LoginInput(string Email, string Password);

public record PostInput(string Url, string Title, string Topic, string Type, string? Comment);

public record ReportInput(string Reason);

public static class Rules
{
    public static bool ValidUrl(string? value) => Uri.TryCreate(value, UriKind.Absolute, out var uri) && (uri.Scheme == "https" || uri.Scheme == "http") && !string.IsNullOrWhiteSpace(uri.Host) && string.IsNullOrEmpty(uri.UserInfo);

}
// Atomic reservation prevents parallel requests overspending. Persist in a transactional ledger before production AI calls.
public sealed class UsageBudget
{
    private readonly int quota;
    private readonly decimal budget;
    public UsageBudget(int quota, decimal budget)
    {
        if (quota < 0 || budget < 0) throw new ArgumentOutOfRangeException(nameof(quota));
        this.quota = quota;
        this.budget = budget;
    }
    private readonly object gate = new();
    private readonly HashSet<Guid> requests = [];
    private int used;
    private decimal spent;

    public bool TryReserve(Guid requestId, decimal estimatedCost)
    {
        lock (gate)
        {
            if (estimatedCost < 0) throw new ArgumentOutOfRangeException(nameof(estimatedCost));
            if (requests.Contains(requestId)) return true;
            if (used >= quota || spent + estimatedCost > budget) return false;
            requests.Add(requestId);
            used++;
            spent += estimatedCost;
            return true;
        }
    }
    public (int Used, decimal Spent) Snapshot()
    {
        lock (gate) return (used, spent);
    }
}
public static class Auth
{
    public static Guid Id(Microsoft.AspNetCore.Http.HttpContext ctx) => Guid.Parse(ctx.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);
}

public static class FinancialRules
{
    public static decimal AiBudget(decimal price, decimal otherVariableCosts, decimal contributionRate)
    {
        if (price < 0 || otherVariableCosts < 0 || contributionRate < 0 || contributionRate > 1) throw new ArgumentOutOfRangeException(nameof(price));
        var available = price * (1 - contributionRate) - otherVariableCosts;
        if (available < 0) throw new ArgumentException("Plano não cobre custos e margem.");
        return decimal.Floor(available * 100) / 100;
    }
    public static (decimal Contribution, decimal Result) Period(decimal revenue, decimal otherVariableCosts, decimal aiCosts, decimal fixedCosts)
    {
        if (revenue < 0 || otherVariableCosts < 0 || aiCosts < 0 || fixedCosts < 0) throw new ArgumentOutOfRangeException(nameof(revenue));
        var contribution = revenue - otherVariableCosts - aiCosts;
        return (contribution, contribution - fixedCosts);
    }
}
