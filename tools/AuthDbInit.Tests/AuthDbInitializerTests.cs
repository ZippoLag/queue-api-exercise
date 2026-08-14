using AuthDbInit;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using QueueApi.Auth;

namespace AuthDbInit.Tests;

/// <summary>
/// Unit tests for <see cref="AuthDbInitializer"/> multi-user seeding.
/// </summary>
public class AuthDbInitializerTests
{
    private const string CmsUsername = "cms-webhook";
    private const string CmsPassword = "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa";
    private const string AdministratorUsername = "administrator";
    private const string AdministratorPassword = "bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb";
    private const string RegularUsername = "regular-user";
    private const string RegularPassword = "cccccccc-cccc-cccc-cccc-cccccccccccc";

    /// <summary>
    /// The three reserved users the APIs expect, in the order the script seeds them.
    /// </summary>
    private static IReadOnlyCollection<UserSeed> ReservedUsers =>
    [
        new(CmsUsername, CmsPassword),
        new(AdministratorUsername, AdministratorPassword),
        new(RegularUsername, RegularPassword),
    ];

    /// <summary>
    /// Verifies a fresh store is created with the schema and all three reserved users are seeded with
    /// hashes that verify against the API's hasher.
    /// </summary>
    /// <remarks>
    /// Source business rule: spec "Credential store is provisioned by an initialization script",
    /// scenario "Initializing a fresh store" — the schema is created and the <c>cms-webhook</c>,
    /// <c>administrator</c> and <c>regular-user</c> users are seeded with the supplied passwords.
    /// </remarks>
    [Fact]
    public async Task InitializeAsync_OnFreshStore_CreatesAllReservedUsers()
    {
        var connectionString = CreateTempConnectionString();
        try
        {
            var results = await AuthDbInitializer.InitializeAsync(connectionString, ReservedUsers);

            results.Should().OnlyContain(result => result.Created);
            results.Select(result => result.Username).Should().Equal(
                CmsUsername, AdministratorUsername, RegularUsername);

            var cms = await FindUserAsync(connectionString, CmsUsername);
            Pbkdf2PasswordHasher.Verify(CmsPassword, cms!.PasswordHash).Should().BeTrue();

            var administrator = await FindUserAsync(connectionString, AdministratorUsername);
            Pbkdf2PasswordHasher.Verify(AdministratorPassword, administrator!.PasswordHash).Should().BeTrue();

            var regular = await FindUserAsync(connectionString, RegularUsername);
            Pbkdf2PasswordHasher.Verify(RegularPassword, regular!.PasswordHash).Should().BeTrue();
        }
        finally
        {
            DeleteTempDatabase(connectionString);
        }
    }

    /// <summary>
    /// Verifies re-running the initializer leaves the store unchanged and does not duplicate users.
    /// </summary>
    /// <remarks>
    /// Source business rule: spec "Credential store is provisioned by an initialization script",
    /// scenario "Re-running the initialization script" — the second run completes without errors and
    /// does not duplicate the seeded users.
    /// </remarks>
    [Fact]
    public async Task InitializeAsync_WhenRunTwice_DoesNotDuplicateUsers()
    {
        var connectionString = CreateTempConnectionString();
        try
        {
            var firstRun = await AuthDbInitializer.InitializeAsync(connectionString, ReservedUsers);
            var secondRun = await AuthDbInitializer.InitializeAsync(connectionString, ReservedUsers);

            firstRun.Should().OnlyContain(result => result.Created);
            secondRun.Should().OnlyContain(result => !result.Created);
            (await CountUsersAsync(connectionString)).Should().Be(3);
        }
        finally
        {
            DeleteTempDatabase(connectionString);
        }
    }

    /// <summary>
    /// Verifies a partially-initialized store only receives the missing users, without duplication.
    /// </summary>
    /// <remarks>
    /// Source business rule: spec scenario "Re-running the initialization script" over a store that
    /// already holds the cms user (e.g. seeded before this change): the administrator and regular-user
    /// are added while cms-webhook is left unchanged, and no user is duplicated.
    /// </remarks>
    [Fact]
    public async Task InitializeAsync_WhenSomeUsersAlreadyExist_SeedsOnlyMissingOnes()
    {
        var connectionString = CreateTempConnectionString();
        try
        {
            await AuthDbInitializer.InitializeAsync(
                connectionString, new[] { new UserSeed(CmsUsername, CmsPassword) });
            var results = await AuthDbInitializer.InitializeAsync(connectionString, ReservedUsers);

            results.Single(result => result.Username == CmsUsername).Created.Should().BeFalse();
            results.Single(result => result.Username == AdministratorUsername).Created.Should().BeTrue();
            results.Single(result => result.Username == RegularUsername).Created.Should().BeTrue();
            (await CountUsersAsync(connectionString)).Should().Be(3);
        }
        finally
        {
            DeleteTempDatabase(connectionString);
        }
    }

    /// <summary>
    /// Verifies re-running with different passwords does not overwrite existing users' hashes.
    /// </summary>
    /// <remarks>
    /// Source business rule: spec "Credential store is provisioned by an initialization script"; the
    /// store leaves existing users unchanged, so the originally seeded passwords keep working.
    /// </remarks>
    [Fact]
    public async Task InitializeAsync_WhenRunWithDifferentPasswords_LeavesExistingUsersUnchanged()
    {
        var connectionString = CreateTempConnectionString();
        try
        {
            await AuthDbInitializer.InitializeAsync(connectionString, ReservedUsers);
            var results = await AuthDbInitializer.InitializeAsync(
                connectionString,
                new[]
                {
                    new UserSeed(CmsUsername, "another-cms-password"),
                    new UserSeed(AdministratorUsername, "another-admin-password"),
                    new UserSeed(RegularUsername, "another-regular-password"),
                });

            results.Should().OnlyContain(result => !result.Created);

            var cms = await FindUserAsync(connectionString, CmsUsername);
            Pbkdf2PasswordHasher.Verify(CmsPassword, cms!.PasswordHash).Should().BeTrue();

            var administrator = await FindUserAsync(connectionString, AdministratorUsername);
            Pbkdf2PasswordHasher.Verify(AdministratorPassword, administrator!.PasswordHash).Should().BeTrue();

            var regular = await FindUserAsync(connectionString, RegularUsername);
            Pbkdf2PasswordHasher.Verify(RegularPassword, regular!.PasswordHash).Should().BeTrue();
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
