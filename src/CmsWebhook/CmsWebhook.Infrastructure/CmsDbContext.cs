using CmsWebhook.Domain;
using Microsoft.EntityFrameworkCore;

namespace CmsWebhook.Infrastructure;

/// <summary>
/// EF Core context for the dedicated CMS database holding the event log (outbox) and the entity store.
/// </summary>
/// <remarks>
/// Design D3/D8: the CMS database is independent of the shared auth credential store, configured via
/// <c>ConnectionStrings:CmsDb</c> and provider-neutral EF Core code (swapping engines later is a provider
/// + connection-string change). The outbox (<c>cms_event_log</c>) is an append-only audit of accepted
/// deliveries; the <c>cms_entities</c> table is the processed state the deferred Users API will read.
/// </remarks>
public class CmsDbContext : DbContext
{
    /// <summary>
    /// Creates the context with the given options.
    /// </summary>
    /// <param name="options">The options carrying the database provider and connection string.</param>
    public CmsDbContext(DbContextOptions<CmsDbContext> options)
        : base(options)
    {
    }

    /// <summary>
    /// The outbox rows of received CMS events.
    /// </summary>
    public DbSet<CmsEvent> Events => Set<CmsEvent>();

    /// <summary>
    /// The processed entity store.
    /// </summary>
    public DbSet<CmsEntity> Entities => Set<CmsEntity>();

    /// <inheritdoc/>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<CmsEvent>(entity =>
        {
            entity.ToTable("cms_event_log");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.EntityId).HasColumnName("entity_id").IsRequired();
            entity.Property(item => item.Type).HasColumnName("event_type").IsRequired().HasConversion<string>().HasMaxLength(16);
            entity.Property(item => item.Version).HasColumnName("version");
            entity.Property(item => item.Payload).HasColumnName("payload_json");
            entity.Property(item => item.Timestamp).HasColumnName("timestamp");
            entity.Property(item => item.ReceivedAt).HasColumnName("received_at");
            entity.Property(item => item.Status).HasColumnName("status").IsRequired().HasConversion<string>().HasMaxLength(16);
            entity.Property(item => item.Error).HasColumnName("error");
            entity.Property(item => item.ProcessedAt).HasColumnName("processed_at");
            entity.HasIndex(item => item.Status);
        });

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
