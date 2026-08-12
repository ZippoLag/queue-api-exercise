using Microsoft.EntityFrameworkCore;

namespace QueueApi.Auth;

/// <summary>
/// EF Core context for the shared database-backed credential store.
/// </summary>
/// <remarks>
/// Spec "Credential store location is configurable": the context is provider-neutral EF Core code
/// configured with the SQLite provider at registration time, so swapping the database engine is a
/// provider + connection-string change (design decision D1). The single <c>Users</c> table is shared
/// by all APIs consuming <c>QueueApi.Auth</c>.
/// </remarks>
public class AuthDbContext : DbContext
{
    /// <summary>
    /// Creates the context with the given options.
    /// </summary>
    /// <param name="options">The options carrying the database provider and connection string.</param>
    public AuthDbContext(DbContextOptions<AuthDbContext> options)
        : base(options)
    {
    }

    /// <summary>
    /// The users registered in the credential store.
    /// </summary>
    public DbSet<UserCredential> Users => Set<UserCredential>();

    /// <inheritdoc/>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<UserCredential>(entity =>
        {
            entity.ToTable("Users");
            entity.HasKey(user => user.Id);
            entity.Property(user => user.Username).HasColumnName("username").IsRequired().HasMaxLength(20);
            entity.HasIndex(user => user.Username).IsUnique();
            entity.Property(user => user.PasswordHash).HasColumnName("password_hash").IsRequired();
        });
    }
}
