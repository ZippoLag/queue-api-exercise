using CmsWebhook.Domain;
using Microsoft.EntityFrameworkCore;

namespace Users.Infrastructure;

/// <summary>
/// EF Core context for the Users API over the shared <c>cms_entities</c> store.
/// </summary>
/// <remarks>
/// Design decision 2 of the users-api-vertical change: the Users module reuses the CmsWebhook entity
/// store directly — no new database. This context maps only the <c>cms_entities</c> table (never the CMS
/// event log); the CmsWebhook module owns the schema, which is created at that API's startup
/// (<c>EnsureCreated</c>), so this API must not create it — doing so would leave the event log missing
/// when the CmsWebhook's <c>EnsureCreated</c> later finds existing tables and skips. Two contexts over
/// one SQLite file follow the standing convention: WAL journal mode and a busy timeout keep the
/// single-writer file coherent.
/// </remarks>
public class UsersDbContext : DbContext
{
    /// <summary>
    /// Creates the context with the given options.
    /// </summary>
    /// <param name="options">The options carrying the database provider and connection string.</param>
    public UsersDbContext(DbContextOptions<UsersDbContext> options)
        : base(options)
    {
    }

    /// <summary>
    /// The published entity store shared with the CmsWebhook module.
    /// </summary>
    public DbSet<CmsEntity> Entities => Set<CmsEntity>();

    /// <inheritdoc/>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // The column mapping must stay byte-for-byte identical to CmsDbContext's so both modules address
        // the same shared table (drift here would silently split the store in two).
        modelBuilder.Entity<CmsEntity>(entity =>
        {
            entity.ToTable("cms_entities");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.LatestVersion).HasColumnName("latest_version");
            entity.Property(item => item.Payload).HasColumnName("payload_json").IsRequired();
            entity.Property(item => item.IsPublished).HasColumnName("is_published");
            entity.Property(item => item.IsVisibleByAdmin).HasColumnName("is_visible_by_admin");
            entity.Property(item => item.UpdatedAt).HasColumnName("updated_at");
        });
    }
}
