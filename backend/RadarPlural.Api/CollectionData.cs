using Microsoft.EntityFrameworkCore;

namespace RadarPlural.Api;

public sealed class CollectionProfile
{
    public Guid MemberId { get; set; }
    public string Topic { get; set; } = "";
    public string[] Keywords { get; set; } = [];
    public string[] ExcludedKeywords { get; set; } = [];
    public string[] SourceIds { get; set; } = [];
    public int IntervalHours { get; set; } = 24;
    public int MaxItems { get; set; } = 10;
    public bool Enabled { get; set; }
    public DateTimeOffset NextRunAt { get; set; }
    public DateTimeOffset? LastRunAt { get; set; }
    public string LastStatus { get; set; } = "pending";
    public string LastError { get; set; } = "";
    public CollectionInput Input() => new(Topic, Keywords, ExcludedKeywords, SourceIds, IntervalHours, MaxItems, Enabled);
}
public sealed class CollectedItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid MemberId { get; set; }
    public string SourceId { get; set; } = "";
    public string SourceName { get; set; } = "";
    public string Url { get; set; } = "";
    public string Title { get; set; } = "";
    public string Summary { get; set; } = "";
    public DateTimeOffset? PublishedAt { get; set; }
    public DateTimeOffset CollectedAt { get; set; } = DateTimeOffset.UtcNow;
}
public static class CollectionData
{
    public static void Configure(ModelBuilder b)
    {
        b.Entity<CollectionProfile>().ToTable("CollectionProfiles").HasKey(x => x.MemberId);
        b.Entity<CollectionProfile>().HasOne<Member>().WithMany().HasForeignKey(x => x.MemberId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<CollectedItem>().ToTable("CollectedItems").HasKey(x => x.Id);
        b.Entity<CollectedItem>().HasIndex(x => new { x.MemberId, x.Url }).IsUnique();
        b.Entity<CollectedItem>().HasOne<Member>().WithMany().HasForeignKey(x => x.MemberId).OnDelete(DeleteBehavior.Cascade);
    }
    // Additive schema for the existing course base. Never drops existing data.
    public const string Schema = """
        CREATE TABLE IF NOT EXISTS "CollectionProfiles" (
          "MemberId" uuid PRIMARY KEY REFERENCES "Members"("Id") ON DELETE CASCADE,
          "Topic" text NOT NULL, "Keywords" text[] NOT NULL, "ExcludedKeywords" text[] NOT NULL,
          "SourceIds" text[] NOT NULL, "IntervalHours" integer NOT NULL, "MaxItems" integer NOT NULL,
          "Enabled" boolean NOT NULL, "NextRunAt" timestamp with time zone NOT NULL,
          "LastRunAt" timestamp with time zone NULL, "LastStatus" text NOT NULL, "LastError" text NOT NULL
        );
        CREATE TABLE IF NOT EXISTS "CollectedItems" (
          "Id" uuid PRIMARY KEY, "MemberId" uuid NOT NULL REFERENCES "Members"("Id") ON DELETE CASCADE,
          "SourceId" text NOT NULL, "SourceName" text NOT NULL, "Url" text NOT NULL, "Title" text NOT NULL,
          "Summary" text NOT NULL, "PublishedAt" timestamp with time zone NULL,
          "CollectedAt" timestamp with time zone NOT NULL
        );
        CREATE UNIQUE INDEX IF NOT EXISTS "IX_CollectedItems_MemberId_Url" ON "CollectedItems" ("MemberId", "Url");
        """;
}
