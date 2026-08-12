using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using QueueApi.Auth;

namespace QueueApi.Auth.Tests;

/// <summary>
/// Unit tests for <see cref="DbUserCredentialsProvider"/> against a real SQLite store.
/// </summary>
public class DbUserCredentialsProviderTests
{
    private const string CmsUsername = "cms-webhook";
    private const string CmsPassword = "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa";

    /// <summary>
    /// Verifies a correct password for a stored user succeeds.
    /// </summary>
    /// <remarks>
    /// Source business rule: spec "Passwords are verified against stored hashes", scenario "Correct password".
    /// </remarks>
    [Fact]
    public async Task VerifyCredentialsAsync_WithCorrectPassword_ReturnsTrue()
    {
        await using var store = await SeededStore.CreateAsync();

        var result = await store.Provider.VerifyCredentialsAsync(CmsUsername, CmsPassword);

        result.Should().BeTrue();
    }

    /// <summary>
    /// Verifies a wrong password for a stored user fails.
    /// </summary>
    /// <remarks>
    /// Source business rule: spec "Passwords are verified against stored hashes", scenario "Incorrect password".
    /// </remarks>
    [Fact]
    public async Task VerifyCredentialsAsync_WithWrongPassword_ReturnsFalse()
    {
        await using var store = await SeededStore.CreateAsync();

        var result = await store.Provider.VerifyCredentialsAsync(CmsUsername, "wrong-password");

        result.Should().BeFalse();
    }

    /// <summary>
    /// Verifies an unknown username fails identically to a wrong password.
    /// </summary>
    /// <remarks>
    /// Source business rule: spec "All endpoints require authentication", scenario "Request with credentials
    /// of an unknown user"; an unknown user is indistinguishable from a wrong password (design decision D3).
    /// </remarks>
    [Fact]
    public async Task VerifyCredentialsAsync_ForUnknownUsername_ReturnsFalse()
    {
        await using var store = await SeededStore.CreateAsync();

        var result = await store.Provider.VerifyCredentialsAsync("no-such-user", CmsPassword);

        result.Should().BeFalse();
    }

    /// <summary>
    /// Verifies an existing user is reported as existing.
    /// </summary>
    /// <remarks>
    /// Source business rule: spec "Credentials are sourced from the credential store"; the startup check
    /// relies on this to fail fast when the store lacks the cms user.
    /// </remarks>
    [Fact]
    public async Task UserExistsAsync_ForExistingUser_ReturnsTrue()
    {
        await using var store = await SeededStore.CreateAsync();

        var result = await store.Provider.UserExistsAsync(CmsUsername);

        result.Should().BeTrue();
    }

    /// <summary>
    /// Verifies a missing user is reported as not existing.
    /// </summary>
    /// <remarks>
    /// Source business rule: spec "Credentials are sourced from the credential store", scenario
    /// "Credential store is not initialized"; the startup check turns a <see langword="false"/> here into a
    /// descriptive startup failure.
    /// </remarks>
    [Fact]
    public async Task UserExistsAsync_ForUnknownUser_ReturnsFalse()
    {
        await using var store = await SeededStore.CreateAsync();

        var result = await store.Provider.UserExistsAsync("no-such-user");

        result.Should().BeFalse();
    }

    /// <summary>
    /// Verifies an initialized-but-empty store surfaces a descriptive error instead of a raw SQL failure.
    /// </summary>
    /// <remarks>
    /// Source business rule: spec "Credentials are sourced from the credential store", scenario
    /// "Credential store is not initialized"; the provider wraps SQLite errors with setup guidance
    /// (design decision D7).
    /// </remarks>
    [Fact]
    public async Task VerifyCredentialsAsync_WhenStoreIsNotInitialized_ThrowsWithGuidance()
    {
        var connectionString = $"Data Source={Path.GetTempFileName()}";
        await using var store = await EmptyStore.CreateAsync(connectionString);

        var act = () => store.Provider.VerifyCredentialsAsync(CmsUsername, CmsPassword);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*scripts/init-db.sh*");
    }

    /// <summary>
    /// Verifies an unreachable store surfaces a descriptive error instead of a raw SQL failure.
    /// </summary>
    /// <remarks>
    /// Source business rule: spec "Credentials are sourced from the credential store", scenario
    /// "Credential store is unreachable"; a connection string whose directory does not exist cannot be opened.
    /// </remarks>
    [Fact]
    public async Task VerifyCredentialsAsync_WhenStoreIsUnreachable_ThrowsWithGuidance()
    {
        var missingDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "missing.db");
        await using var store = await EmptyStore.CreateAsync($"Data Source={missingDirectory}");

        var act = () => store.Provider.VerifyCredentialsAsync(CmsUsername, CmsPassword);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*scripts/init-db.sh*");
    }

    /// <summary>
    /// An in-memory SQLite store seeded with the cms user.
    /// </summary>
    private sealed class SeededStore : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly AuthDbContext _context;

        private SeededStore(SqliteConnection connection, AuthDbContext context, DbUserCredentialsProvider provider)
        {
            _connection = connection;
            _context = context;
            Provider = provider;
        }

        /// <summary>
        /// The provider under test.
        /// </summary>
        public DbUserCredentialsProvider Provider { get; }

        /// <summary>
        /// Creates an in-memory store whose schema is created and seeded with the cms user.
        /// </summary>
        /// <returns>The disposable store wrapper.</returns>
        public static async Task<SeededStore> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var options = new DbContextOptionsBuilder<AuthDbContext>().UseSqlite(connection).Options;
            var context = new AuthDbContext(options);
            await context.Database.EnsureCreatedAsync();
            context.Users.Add(new UserCredential
            {
                Username = CmsUsername,
                PasswordHash = Pbkdf2PasswordHasher.Hash(CmsPassword),
            });
            await context.SaveChangesAsync();
            return new SeededStore(connection, context, new DbUserCredentialsProvider(context));
        }

        /// <inheritdoc/>
        public async ValueTask DisposeAsync()
        {
            await _context.DisposeAsync();
            await _connection.DisposeAsync();
        }
    }

    /// <summary>
    /// A provider bound to a raw connection string without schema creation.
    /// </summary>
    private sealed class EmptyStore : IAsyncDisposable
    {
        private readonly AuthDbContext _context;

        private EmptyStore(AuthDbContext context, DbUserCredentialsProvider provider)
        {
            _context = context;
            Provider = provider;
        }

        /// <summary>
        /// The provider under test.
        /// </summary>
        public DbUserCredentialsProvider Provider { get; }

        /// <summary>
        /// Creates a provider over the given connection string without creating any schema.
        /// </summary>
        /// <param name="connectionString">The SQLite connection string to point the provider at.</param>
        /// <returns>The disposable store wrapper.</returns>
        public static Task<EmptyStore> CreateAsync(string connectionString)
        {
            var context = new AuthDbContext(
                new DbContextOptionsBuilder<AuthDbContext>().UseSqlite(connectionString).Options);
            return Task.FromResult(new EmptyStore(context, new DbUserCredentialsProvider(context)));
        }

        /// <inheritdoc/>
        public async ValueTask DisposeAsync()
        {
            await _context.DisposeAsync();
        }
    }
}
