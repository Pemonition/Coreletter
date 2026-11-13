using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using RadarPlural.Domain.Members;
using RadarPlural.Domain.News;
using RadarPlural.Domain.Posts;

namespace RadarPlural.Infrastructure.Persistence;

public sealed class AppDb(DbContextOptions<AppDb> options) : DbContext(options), IDataProtectionKeyContext
{
    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();
    public DbSet<Member> Members => Set<Member>();
    public DbSet<Post> Posts => Set<Post>();
    public DbSet<SavedPost> Saved => Set<SavedPost>();
    public DbSet<Report> Reports => Set<Report>();
    public DbSet<FeedItem> FeedItems => Set<FeedItem>();

    // Mapeamento idêntico ao anterior à reestruturação: nenhuma migração nova é necessária
    // (o teste MigrationsCoverTheModel garante isso).
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
        });
    }
}
