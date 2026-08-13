using CmsWebhook.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace CmsWebhook.Infrastructure.Tests;

/// <summary>
/// A throwaway SQLite database for infrastructure tests, created and cleaned up per test.
/// </summary>
/// <remarks>
/// Uses a temporary file (like the shared auth tests) so multi-connection scenarios such as the worker
/// sweep behave like production instead of relying on shared in-memory connections.
/// </remarks>
public sealed class CmsTestDatabase : IDisposable
{
    private readonly string _databasePath;

    /// <summary>
    /// Creates a fresh database file with the CMS schema.
    /// </summary>
    public CmsTestDatabase()
    {
        _databasePath = Path.Combine(Path.GetTempPath(), $"queue-api-cms-tests-{Guid.NewGuid():N}.db");
        ConnectionString = $"Data Source={_databasePath}";

        using var context = CreateContext();
        context.Database.EnsureCreated();
    }

    /// <summary>
    /// The connection string for the fresh database.
    /// </summary>
    public string ConnectionString { get; }

    /// <summary>
    /// Creates a new <see cref="CmsDbContext"/> over this database.
    /// </summary>
    /// <returns>A new context instance.</returns>
    public CmsDbContext CreateContext()
        => new(new DbContextOptionsBuilder<CmsDbContext>().UseSqlite(ConnectionString).Options);

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
