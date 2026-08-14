using Microsoft.EntityFrameworkCore;

namespace Users.Infrastructure.Tests;

/// <summary>
/// A throwaway SQLite database for Users infrastructure tests, created and cleaned up per test.
/// </summary>
/// <remarks>
/// Uses a temporary file (like the shared auth and CMS tests) so multi-connection scenarios behave like
/// production instead of relying on shared in-memory connections. The schema is created with the Users
/// context, which maps exactly the shared <c>cms_entities</c> table the API reads.
/// </remarks>
public sealed class UsersTestDatabase : IDisposable
{
    private readonly string _databasePath;

    /// <summary>
    /// Creates a fresh database file with the <c>cms_entities</c> schema.
    /// </summary>
    public UsersTestDatabase()
    {
        _databasePath = Path.Combine(Path.GetTempPath(), $"queue-api-users-tests-{Guid.NewGuid():N}.db");
        ConnectionString = $"Data Source={_databasePath}";

        using var context = CreateContext();
        context.Database.EnsureCreated();
    }

    /// <summary>
    /// The connection string for the fresh database.
    /// </summary>
    public string ConnectionString { get; }

    /// <summary>
    /// Creates a new <see cref="UsersDbContext"/> over this database.
    /// </summary>
    /// <returns>A new context instance.</returns>
    public UsersDbContext CreateContext()
        => new(new DbContextOptionsBuilder<UsersDbContext>().UseSqlite(ConnectionString).Options);

    /// <inheritdoc/>
    public void Dispose()
    {
        foreach (var suffix in new[] { string.Empty, "-wal", "-shm" })
        {
            var candidate = _databasePath + suffix;
            if (File.Exists(candidate))
            {
                File.Delete(candidate);
            }
        }
    }
}
