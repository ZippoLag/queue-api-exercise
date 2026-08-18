using CmsWebhook.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using QueueApi.Auth;

namespace CmsWebhook.Api.Tests;

/// <summary>
/// Test host for the CMS Webhook API that runs against seeded temporary SQLite stores and can swap the
/// credential provider.
/// </summary>
/// <remarks>
/// Both stores are replaced: the credential store (seeded with the cms user) and the CMS event database
/// (empty, created on the fly). The outbox worker runs against the same temporary CMS store, so tests can
/// observe recorded events being processed asynchronously.
/// </remarks>
public class CmsWebhookApiFactory : WebApplicationFactory<Program>
{
    /// <summary>
    /// The cms username seeded into the temporary store, mirroring the default configuration.
    /// </summary>
    public const string CmsUsername = "cms-webhook";

    /// <summary>
    /// The cms password seeded into the temporary store.
    /// </summary>
    public const string CmsPassword = "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa";

    private readonly string _authDbConnectionString;
    private readonly string _cmsDbConnectionString;
    private readonly IUserCredentialsProvider? _credentialsProviderOverride;
    private readonly int? _rateLimitPermitLimit;
    private readonly string? _temporaryDatabasePath;
    private readonly string? _cmsTemporaryDatabasePath;

    /// <summary>
    /// Creates the factory, optionally pointing at specific stores and/or swapping the provider.
    /// </summary>
    /// <param name="authDbConnectionString">
    /// The SQLite connection string for the credential store; when <see langword="null"/> a temporary
    /// store seeded with the cms user is created and cleaned up with the factory.
    /// </param>
    /// <param name="credentialsProviderOverride">The provider to use instead of the DB-backed one, or <see langword="null"/>.</param>
    /// <param name="cmsDbConnectionString">
    /// The SQLite connection string for the CMS event database; when <see langword="null"/> a temporary
    /// empty store is created and cleaned up with the factory.
    /// </param>
    /// <param name="rateLimitPermitLimit">
    /// Overrides <c>RateLimiting:PermitLimit</c> so rate-limit tests exercise a small window without
    /// sending the production default volume of requests; <see langword="null"/> keeps the configured default.
    /// </param>
    public CmsWebhookApiFactory(
        string? authDbConnectionString = null,
        IUserCredentialsProvider? credentialsProviderOverride = null,
        string? cmsDbConnectionString = null,
        int? rateLimitPermitLimit = null)
    {
        _credentialsProviderOverride = credentialsProviderOverride;
        _rateLimitPermitLimit = rateLimitPermitLimit;
        if (authDbConnectionString is null)
        {
            _temporaryDatabasePath = CreateSeededTempDatabase(out var authConnectionString);
            _authDbConnectionString = authConnectionString;
        }
        else
        {
            _authDbConnectionString = authDbConnectionString;
        }

        if (cmsDbConnectionString is null)
        {
            _cmsTemporaryDatabasePath = CreateEmptyCmsDatabase(out var cmsConnectionString);
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
        if (_rateLimitPermitLimit is not null)
        {
            // UseSetting feeds the host configuration, which is merged into the app's configuration
            // before Program.cs's top-level statements read RateLimiting:PermitLimit — a
            // ConfigureAppConfiguration override would arrive too late for that pre-Build read.
            builder.UseSetting("RateLimiting:PermitLimit", _rateLimitPermitLimit.Value.ToString());
        }

        builder.ConfigureServices(services =>
        {
            // Replaces the DbContexts registered by the app so every request (and the startup fail-fast
            // checks) talk to the test stores; service overrides run after Program.cs's registrations, so
            // these swaps are authoritative. Both the contexts and their options are removed so the test
            // stores' options are the only registrations left.
            services.RemoveAll<AuthDbContext>();
            services.RemoveAll<DbContextOptions<AuthDbContext>>();
            services.AddDbContext<AuthDbContext>(options => options.UseSqlite(_authDbConnectionString));

            services.RemoveAll<CmsDbContext>();
            services.RemoveAll<DbContextOptions<CmsDbContext>>();
            services.AddDbContext<CmsDbContext>(options => options.UseSqlite(_cmsDbConnectionString));

            if (_credentialsProviderOverride is not null)
            {
                services.RemoveAll<IUserCredentialsProvider>();
                services.AddScoped<IUserCredentialsProvider>(_ => _credentialsProviderOverride);
            }
        });
    }

    /// <summary>
    /// Creates a context over the test CMS database for asserting recorded/processed state.
    /// </summary>
    /// <returns>A <see cref="CmsDbContext"/> pointing at the temporary CMS store.</returns>
    public CmsDbContext CreateCmsDbContext()
        => new(new DbContextOptionsBuilder<CmsDbContext>().UseSqlite(_cmsDbConnectionString).Options);

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        DeleteDatabaseFiles(_temporaryDatabasePath);
        DeleteDatabaseFiles(_cmsTemporaryDatabasePath);
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

    private static string CreateSeededTempDatabase(out string connectionString)
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"queue-api-auth-tests-{Guid.NewGuid():N}.db");
        connectionString = $"Data Source={databasePath}";

        var options = new DbContextOptionsBuilder<AuthDbContext>().UseSqlite(connectionString).Options;
        using var context = new AuthDbContext(options);
        context.Database.EnsureCreated();
        context.Users.Add(new UserCredential
        {
            Username = CmsUsername,
            PasswordHash = Pbkdf2PasswordHasher.Hash(CmsPassword),
        });
        context.SaveChanges();

        return databasePath;
    }

    private static string CreateEmptyCmsDatabase(out string connectionString)
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"queue-api-cms-api-tests-{Guid.NewGuid():N}.db");
        connectionString = $"Data Source={databasePath}";

        var options = new DbContextOptionsBuilder<CmsDbContext>().UseSqlite(connectionString).Options;
        using var context = new CmsDbContext(options);
        context.Database.EnsureCreated();

        return databasePath;
    }
}
