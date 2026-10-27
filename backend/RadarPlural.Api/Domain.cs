using Microsoft.EntityFrameworkCore;

namespace RadarPlural.Api;


public sealed class AppDb(DbContextOptions<AppDb> options) : DbContext(options)
{
    public DbSet<Member> Members => Set<Member>();
    public DbSet<Post> Posts => Set<Post>();
    public DbSet<SavedPost> Saved => Set<SavedPost>();
    public DbSet<Report> Reports => Set<Report>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Member>().HasIndex(x => x.Email).IsUnique();
        b.Entity<SavedPost>().HasKey(x => new { x.MemberId, x.PostId });
        b.Entity<SavedPost>().HasOne<Post>().WithMany().HasForeignKey(x => x.PostId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<Report>().HasOne<Post>().WithMany().HasForeignKey(x => x.PostId).OnDelete(DeleteBehavior.Cascade);
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
