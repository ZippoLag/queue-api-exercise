using CmsWebhook.Domain;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using QueueApi.Auth;
using Users.Infrastructure;

namespace Users.Api.Tests;

/// <summary>
/// Test host for the Users API that runs against seeded temporary SQLite stores.
/// </summary>
/// <remarks>
/// Both stores are replaced: the credential store (seeded with the <c>cms-webhook</c>, <c>administrator</c>
/// and <c>regular-user</c> users) and the shared CMS store (holding the <c>cms_entities</c> table, empty
/// and created on the fly). Tests seed entities through <see cref="SeedEntities"/> so each test controls
/// the store contents it lists or toggles.
/// </remarks>
public class UsersApiFactory : WebApplicationFactory<Program>
{
    /// <summary>
    /// The reserved cms username, rejected on the Users API.
    /// </summary>
    public const string CmsUsername = "cms-webhook";

    /// <summary>
    /// The reserved administrator username, the only user authorized for enable/disable.
    /// </summary>
    public const string AdministratorUsername = "administrator";

    /// <summary>
    /// The reserved regular-user username.
    /// </summary>
    public const string RegularUsername = "regular-user";

    /// <summary>
    /// The cms user's password seeded into the temporary store.
    /// </summary>
    public const string CmsPassword = "cccccccc-cccc-cccc-cccc-cccccccccccc";

    /// <summary>
    /// The administrator's password seeded into the temporary store.
    /// </summary>
    public const string AdministratorPassword = "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa";

    /// <summary>
    /// The regular user's password seeded into the temporary store.
    /// </summary>
    public const string RegularPassword = "bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb";

    private readonly string _authDbConnectionString;
    private readonly string _cmsDbConnectionString;
    private readonly string? _temporaryAuthDatabasePath;
    private readonly string? _temporaryCmsDatabasePath;

    /// <summary>
    /// Creates the factory, optionally pointing at specific stores.
    /// </summary>
    /// <param name="authDbConnectionString">
    /// The SQLite connection string for the credential store; when <see langword="null"/> a temporary
    /// store seeded with the three reserved users is created and cleaned up with the factory.
    /// </param>
    /// <param name="cmsDbConnectionString">
    /// The SQLite connection string for the shared CMS store; when <see langword="null"/> a temporary
    /// store with the <c>cms_entities</c> schema is created and cleaned up with the factory.
    /// </param>
    public UsersApiFactory(string? authDbConnectionString = null, string? cmsDbConnectionString = null)
    {
        if (authDbConnectionString is null)
        {
            _temporaryAuthDatabasePath = CreateSeededAuthDatabase(out var authConnectionString);
            _authDbConnectionString = authConnectionString;
        }
        else
        {
            _authDbConnectionString = authDbConnectionString;
        }

        if (cmsDbConnectionString is null)
        {
            _temporaryCmsDatabasePath = CreateEmptyCmsDatabase(out var cmsConnectionString);
            _cmsDbConnectionString = cmsConnectionString;
        }
        else
        {
            _cmsDbConnectionString = cmsDbConnectionString;
        }
    }

    /// <inheritdoc/>
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // Replaces the DbContexts registered by the app so every request (and the startup fail-fast
            // check) talk to the test stores; service overrides run after Program.cs's registrations, so
            // these swaps are authoritative. Both the contexts and their options are removed so the test
            // stores' options are the only registrations left.
            services.RemoveAll<AuthDbContext>();
            services.RemoveAll<DbContextOptions<AuthDbContext>>();
            services.AddDbContext<AuthDbContext>(options => options.UseSqlite(_authDbConnectionString));

            services.RemoveAll<UsersDbContext>();
            services.RemoveAll<DbContextOptions<UsersDbContext>>();
            services.AddDbContext<UsersDbContext>(options => options.UseSqlite(_cmsDbConnectionString));
        });
    }

    /// <summary>
    /// Creates a context over the test CMS store for seeding and asserting entity state.
    /// </summary>
    /// <returns>A <see cref="UsersDbContext"/> pointing at the temporary CMS store.</returns>
    public UsersDbContext CreateUsersDbContext()
        => new(new DbContextOptionsBuilder<UsersDbContext>().UseSqlite(_cmsDbConnectionString).Options);

    /// <summary>
    /// Inserts the given entities into the test entity store.
    /// </summary>
    /// <param name="entities">The entities to persist.</param>
    public void SeedEntities(params CmsEntity[] entities)
    {
        using var context = CreateUsersDbContext();
        context.Entities.AddRange(entities);
        context.SaveChanges();
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        DeleteDatabaseFiles(_temporaryAuthDatabasePath);
        DeleteDatabaseFiles(_temporaryCmsDatabasePath);
    }

    private static void DeleteDatabaseFiles(string? databasePath)
    {
        if (databasePath is null)
        {
            return;
        }

        foreach (var suffix in new[] { string.Empty, "-wal", "-shm" })
        {
            var candidate = databasePath + suffix;
            if (File.Exists(candidate))
            {
                File.Delete(candidate);
            }
        }
    }

    private static string CreateSeededAuthDatabase(out string connectionString)
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"queue-api-users-auth-tests-{Guid.NewGuid():N}.db");
        connectionString = $"Data Source={databasePath}";

        var options = new DbContextOptionsBuilder<AuthDbContext>().UseSqlite(connectionString).Options;
        using var context = new AuthDbContext(options);
        context.Database.EnsureCreated();
        context.Users.AddRange(
            new UserCredential { Username = CmsUsername, PasswordHash = Pbkdf2PasswordHasher.Hash(CmsPassword) },
            new UserCredential { Username = AdministratorUsername, PasswordHash = Pbkdf2PasswordHasher.Hash(AdministratorPassword) },
            new UserCredential { Username = RegularUsername, PasswordHash = Pbkdf2PasswordHasher.Hash(RegularPassword) });
        context.SaveChanges();

        return databasePath;
    }

    private static string CreateEmptyCmsDatabase(out string connectionString)
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"queue-api-users-cms-tests-{Guid.NewGuid():N}.db");
        connectionString = $"Data Source={databasePath}";

        var options = new DbContextOptionsBuilder<UsersDbContext>().UseSqlite(connectionString).Options;
        using var context = new UsersDbContext(options);
        context.Database.EnsureCreated();

        return databasePath;
    }
}
