using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using QueueApi.Auth;

namespace CmsWebhook.Api.Tests;

/// <summary>
/// Test host for the CMS Webhook API that runs against a seeded temporary SQLite store and can
/// swap the credential provider.
/// </summary>
/// <remarks>
/// Spec "Credentials are sourced from the credential store": every test runs the real DB-backed flow
/// against a throwaway store seeded with the cms user, so the suite never depends on a checked-in
/// database. Spec "Only the cms user is authorized": the non-cms authorized user for the 403 path is
/// injected through the provider seam (design decision D3).
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
    private readonly IUserCredentialsProvider? _credentialsProviderOverride;
    private readonly string? _temporaryDatabasePath;

    /// <summary>
    /// Creates the factory, optionally pointing at a specific credential store and/or swapping the provider.
    /// </summary>
    /// <param name="authDbConnectionString">
    /// The SQLite connection string for the credential store; when <see langword="null"/> a temporary
    /// store seeded with the cms user is created and cleaned up with the factory.
    /// </param>
    /// <param name="credentialsProviderOverride">The provider to use instead of the DB-backed one, or <see langword="null"/>.</param>
    public CmsWebhookApiFactory(
        string? authDbConnectionString = null,
        IUserCredentialsProvider? credentialsProviderOverride = null)
    {
        _credentialsProviderOverride = credentialsProviderOverride;
        if (authDbConnectionString is null)
        {
            _temporaryDatabasePath = CreateSeededTempDatabase(out var connectionString);
            _authDbConnectionString = connectionString;
        }
        else
        {
            _authDbConnectionString = authDbConnectionString;
        }
    }

    /// <inheritdoc/>
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // Replaces the DbContext registered by the app so every request (and the startup
            // fail-fast check) talks to the test store; service overrides run after Program.cs's
            // registrations, so this swap is authoritative. Both the context and its options are
            // removed so the test store's options are the only registration left.
            services.RemoveAll<AuthDbContext>();
            services.RemoveAll<DbContextOptions<AuthDbContext>>();
            services.AddDbContext<AuthDbContext>(options => options.UseSqlite(_authDbConnectionString));

            if (_credentialsProviderOverride is not null)
            {
                services.RemoveAll<IUserCredentialsProvider>();
                services.AddScoped<IUserCredentialsProvider>(_ => _credentialsProviderOverride);
            }
        });
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (_temporaryDatabasePath is not null)
        {
            foreach (var suffix in new[] { string.Empty, "-wal", "-shm" })
            {
                var candidate = _temporaryDatabasePath + suffix;
                if (File.Exists(candidate))
                {
                    File.Delete(candidate);
                }
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
}
