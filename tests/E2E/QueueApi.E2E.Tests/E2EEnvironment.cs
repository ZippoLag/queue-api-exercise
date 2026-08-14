using System.Net.Http.Headers;
using System.Text;
using Microsoft.EntityFrameworkCore;
using QueueApi.Auth;

namespace QueueApi.E2E.Tests;

/// <summary>
/// The end-to-end test environment: one seeded credential store and one CMS database shared by both
/// API hosts, exactly as a deployment would run them side by side.
/// </summary>
/// <remarks>
/// The credential store is seeded with the three reserved users and the CMS database is created by the
/// CMS Webhook host's startup fail-fast (<c>EnsureCreated</c> + WAL), so the schema lands on the same
/// file the Users host reads. The CMS host must start before the Users host is exercised: its startup
/// provisions the <c>cms_entities</c> table on the shared file.
/// </remarks>
public sealed class E2EEnvironment : IDisposable
{
    /// <summary>
    /// The reserved cms username, allowed only on the CMS Webhook API.
    /// </summary>
    public const string CmsUsername = "cms-webhook";

    /// <summary>
    /// The reserved administrator username, allowed to enable/disable visibility.
    /// </summary>
    public const string AdministratorUsername = "administrator";

    /// <summary>
    /// The reserved regular-user username.
    /// </summary>
    public const string RegularUsername = "regular-user";

    /// <summary>
    /// The cms user's password seeded into the shared store.
    /// </summary>
    public const string CmsPassword = "cccccccc-cccc-cccc-cccc-cccccccccccc";

    /// <summary>
    /// The administrator's password seeded into the shared store.
    /// </summary>
    public const string AdministratorPassword = "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa";

    /// <summary>
    /// The regular user's password seeded into the shared store.
    /// </summary>
    public const string RegularPassword = "bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb";

    private readonly string _authDatabasePath;
    private readonly string _cmsDatabasePath;

    /// <summary>
    /// The CMS Webhook host over the shared stores.
    /// </summary>
    public CmsHost CmsApi { get; }

    /// <summary>
    /// The Users API host over the shared stores.
    /// </summary>
    public UsersHost UsersApi { get; }

    /// <summary>
    /// An anonymous client for the CMS Webhook API (health/OpenAPI probes).
    /// </summary>
    public HttpClient CmsClient { get; }

    /// <summary>
    /// An anonymous client for the Users API (health/OpenAPI probes).
    /// </summary>
    public HttpClient UsersClient { get; }

    /// <summary>
    /// Creates the shared stores and starts both API hosts.
    /// </summary>
    public E2EEnvironment()
    {
        _authDatabasePath = CreateSeededAuthDatabase(out var authConnectionString);
        _cmsDatabasePath = Path.Combine(Path.GetTempPath(), $"queue-api-e2e-cms-{Guid.NewGuid():N}.db");
        var cmsConnectionString = $"Data Source={_cmsDatabasePath}";

        CmsApi = new CmsHost(authConnectionString, cmsConnectionString);
        CmsClient = CmsApi.CreateClient();

        UsersApi = new UsersHost(authConnectionString, cmsConnectionString);
        UsersClient = UsersApi.CreateClient();
    }

    /// <summary>
    /// Creates a CMS Webhook API client authenticated as the given user.
    /// </summary>
    /// <param name="username">The username to authenticate with.</param>
    /// <param name="password">The user's password.</param>
    /// <returns>A new client with the Basic credentials attached.</returns>
    public HttpClient CreateCmsApiClient(string username, string password)
        => Authenticated(CmsApi.CreateClient(), username, password);

    /// <summary>
    /// Creates a Users API client authenticated as the given user.
    /// </summary>
    /// <param name="username">The username to authenticate with.</param>
    /// <param name="password">The user's password.</param>
    /// <returns>A new client with the Basic credentials attached.</returns>
    public HttpClient CreateUsersApiClient(string username, string password)
        => Authenticated(UsersApi.CreateClient(), username, password);

    /// <inheritdoc/>
    public void Dispose()
    {
        UsersApi.Dispose();
        CmsApi.Dispose();
        DeleteDatabaseFiles(_authDatabasePath);
        DeleteDatabaseFiles(_cmsDatabasePath);
    }

    private static HttpClient Authenticated(HttpClient client, string username, string password)
    {
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Basic",
            Convert.ToBase64String(Encoding.UTF8.GetBytes($"{username}:{password}")));
        return client;
    }

    private static void DeleteDatabaseFiles(string databasePath)
    {
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
        var databasePath = Path.Combine(Path.GetTempPath(), $"queue-api-e2e-auth-{Guid.NewGuid():N}.db");
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
}
