using AuthDbInit;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using QueueApi.Auth;

namespace AuthDbInit.Tests;

/// <summary>
/// Unit tests for <see cref="AuthDbInitializer"/>.
/// </summary>
public class AuthDbInitializerTests
{
    private const string CmsUsername = "cms-webhook";
    private const string CmsPassword = "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa";

    /// <summary>
    /// Verifies a fresh store is created with the schema and the user is seeded with a verifiable hash.
    /// </summary>
    /// <remarks>
    /// Source business rule: spec "Credential store is provisioned by an initialization script",
    /// scenario "Initializing a fresh store"; the seeded hash must verify against the API's hasher.
    /// </remarks>
    [Fact]
    public async Task InitializeAsync_OnFreshStore_CreatesSeededUser()
    {
        var connectionString = CreateTempConnectionString();
        try
        {
            var result = await AuthDbInitializer.InitializeAsync(connectionString, CmsUsername, CmsPassword);

            result.Should().Be(InitializationResult.Created);
            var user = await FindUserAsync(connectionString, CmsUsername);
            user.Should().NotBeNull();
            Pbkdf2PasswordHasher.Verify(CmsPassword, user!.PasswordHash).Should().BeTrue();
        }
        finally
        {
            DeleteTempDatabase(connectionString);
        }
    }

    /// <summary>
    /// Verifies re-running the initializer leaves the store unchanged and does not duplicate the user.
    /// </summary>
    /// <remarks>
    /// Source business rule: spec "Credential store is provisioned by an initialization script",
    /// scenario "Re-running the initialization script".
    /// </remarks>
    [Fact]
    public async Task InitializeAsync_WhenRunTwice_DoesNotDuplicateUser()
    {
        var connectionString = CreateTempConnectionString();
        try
        {
            var firstRun = await AuthDbInitializer.InitializeAsync(connectionString, CmsUsername, CmsPassword);
            var secondRun = await AuthDbInitializer.InitializeAsync(connectionString, CmsUsername, CmsPassword);

            firstRun.Should().Be(InitializationResult.Created);
            secondRun.Should().Be(InitializationResult.AlreadyExists);
            (await CountUsersAsync(connectionString)).Should().Be(1);
        }
        finally
        {
            DeleteTempDatabase(connectionString);
        }
    }

    /// <summary>
    /// Verifies re-running with a different password does not overwrite the existing user's hash.
    /// </summary>
    /// <remarks>
    /// Source business rule: spec "Credential store is provisioned by an initialization script"; the
    /// store leaves existing users unchanged, so the originally seeded password keeps working.
    /// </remarks>
    [Fact]
    public async Task InitializeAsync_WhenRunWithDifferentPassword_LeavesExistingUserUnchanged()
    {
        var connectionString = CreateTempConnectionString();
        try
        {
            await AuthDbInitializer.InitializeAsync(connectionString, CmsUsername, CmsPassword);
            var result = await AuthDbInitializer.InitializeAsync(connectionString, CmsUsername, "another-password");

            result.Should().Be(InitializationResult.AlreadyExists);
            var user = await FindUserAsync(connectionString, CmsUsername);
            Pbkdf2PasswordHasher.Verify(CmsPassword, user!.PasswordHash).Should().BeTrue();
        }
        finally
        {
            DeleteTempDatabase(connectionString);
        }
    }

    private static string CreateTempConnectionString()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"auth-db-init-tests-{Guid.NewGuid():N}.db");
        return $"Data Source={databasePath}";
    }

    private static async Task<UserCredential?> FindUserAsync(string connectionString, string username)
    {
        var options = new DbContextOptionsBuilder<AuthDbContext>().UseSqlite(connectionString).Options;
        await using var context = new AuthDbContext(options);
        return await context.Users.SingleOrDefaultAsync(user => user.Username == username);
    }

    private static async Task<int> CountUsersAsync(string connectionString)
    {
        var options = new DbContextOptionsBuilder<AuthDbContext>().UseSqlite(connectionString).Options;
        await using var context = new AuthDbContext(options);
        return await context.Users.CountAsync();
    }

    private static void DeleteTempDatabase(string connectionString)
    {
        var dataSource = new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder(connectionString).DataSource;
        foreach (var suffix in new[] { string.Empty, "-wal", "-shm" })
        {
            var candidate = dataSource + suffix;
            if (File.Exists(candidate))
            {
                File.Delete(candidate);
            }
        }
    }
}
