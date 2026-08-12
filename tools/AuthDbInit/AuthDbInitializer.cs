using Microsoft.EntityFrameworkCore;
using QueueApi.Auth;

namespace AuthDbInit;

/// <summary>
/// Initializes the shared credential store schema and seeds a user, idempotently.
/// </summary>
/// <remarks>
/// Spec "Credential store is provisioned by an initialization script": the schema is created when missing
/// (<c>EnsureCreated</c>, per design decision D6 — no migration infrastructure yet) and a user is inserted
/// only when the username is absent, so re-running never duplicates data. Passwords are hashed with the
/// exact helper the APIs verify against (<see cref="Pbkdf2PasswordHasher"/>), keeping seeded hashes
/// compatible (design decision D6).
/// </remarks>
public static class AuthDbInitializer
{
    /// <summary>
    /// Ensures the store schema exists and seeds <paramref name="username"/> when missing.
    /// </summary>
    /// <param name="connectionString">The SQLite connection string of the credential store.</param>
    /// <param name="username">The username to seed.</param>
    /// <param name="password">The plaintext password to hash and store.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns><see cref="InitializationResult.Created"/> when the user was seeded, <see cref="InitializationResult.AlreadyExists"/> otherwise.</returns>
    /// <exception cref="System.Data.Common.DbException">
    /// The store could not be reached while creating the schema or seeding the user (e.g. the
    /// database file is missing or locked).
    /// </exception>
    public static async Task<InitializationResult> InitializeAsync(
        string connectionString,
        string username,
        string password,
        CancellationToken cancellationToken = default)
    {
        var options = new DbContextOptionsBuilder<AuthDbContext>().UseSqlite(connectionString).Options;
        await using var context = new AuthDbContext(options);

        await context.Database.EnsureCreatedAsync(cancellationToken);

        var userExists = await context.Users.AnyAsync(user => user.Username == username, cancellationToken);
        if (userExists)
        {
            return InitializationResult.AlreadyExists;
        }

        context.Users.Add(new UserCredential
        {
            Username = username,
            PasswordHash = Pbkdf2PasswordHasher.Hash(password),
        });
        await context.SaveChangesAsync(cancellationToken);

        return InitializationResult.Created;
    }
}

/// <summary>
/// The outcome of a credential store initialization run.
/// </summary>
public enum InitializationResult
{
    /// <summary>
    /// The user was created by this run.
    /// </summary>
    Created,

    /// <summary>
    /// The user already existed; the store was left unchanged.
    /// </summary>
    AlreadyExists,
}
