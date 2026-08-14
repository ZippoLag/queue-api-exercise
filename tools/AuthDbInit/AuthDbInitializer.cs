using Microsoft.EntityFrameworkCore;
using QueueApi.Auth;

namespace AuthDbInit;

/// <summary>
/// A reserved username/password pair to seed into the shared credential store.
/// </summary>
/// <param name="Username">The reserved username (e.g. <c>cms-webhook</c>, <c>administrator</c>, <c>regular-user</c>).</param>
/// <param name="Password">The plaintext password to hash and store.</param>
public sealed record UserSeed(string Username, string Password);

/// <summary>
/// The outcome of seeding one user during an initialization run.
/// </summary>
/// <param name="Username">The seeded username.</param>
/// <param name="Created">Whether this run created the user; <see langword="false"/> when it already existed.</param>
public sealed record UserSeedResult(string Username, bool Created);

/// <summary>
/// Initializes the shared credential store schema and seeds the reserved users, idempotently.
/// </summary>
/// <remarks>
/// Spec "Credential store is provisioned by an initialization script": the schema is created when missing
/// (<c>EnsureCreated</c>, per design decision D6 — no migration infrastructure yet) and a user is inserted
/// only when the username is absent, so re-running never duplicates data. Passwords are hashed with the
/// exact helper the APIs verify against (<see cref="Pbkdf2PasswordHasher"/>), keeping seeded hashes
/// compatible (design decision D6). The reserved usernames themselves are chosen by the caller — the CLI
/// passes the <c>cms-webhook</c>, <c>administrator</c> and <c>regular-user</c> names the APIs expect.
/// </remarks>
public static class AuthDbInitializer
{
    /// <summary>
    /// Ensures the store schema exists and seeds every requested user that is missing.
    /// </summary>
    /// <param name="connectionString">The SQLite connection string of the credential store.</param>
    /// <param name="users">The username/password pairs to seed; existing users are left unchanged.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>One <see cref="UserSeedResult"/> per requested user, in the same order.</returns>
    /// <exception cref="System.Data.Common.DbException">
    /// The store could not be reached while creating the schema or seeding the users (e.g. the
    /// database file is missing or locked).
    /// </exception>
    public static async Task<IReadOnlyList<UserSeedResult>> InitializeAsync(
        string connectionString,
        IReadOnlyCollection<UserSeed> users,
        CancellationToken cancellationToken = default)
    {
        var options = new DbContextOptionsBuilder<AuthDbContext>().UseSqlite(connectionString).Options;
        await using var context = new AuthDbContext(options);

        await context.Database.EnsureCreatedAsync(cancellationToken);

        var results = new List<UserSeedResult>(users.Count);
        foreach (var user in users)
        {
            var userExists = await context.Users.AnyAsync(item => item.Username == user.Username, cancellationToken);
            if (userExists)
            {
                results.Add(new UserSeedResult(user.Username, Created: false));
                continue;
            }

            context.Users.Add(new UserCredential
            {
                Username = user.Username,
                PasswordHash = Pbkdf2PasswordHasher.Hash(user.Password),
            });
            results.Add(new UserSeedResult(user.Username, Created: true));
        }

        await context.SaveChangesAsync(cancellationToken);

        return results;
    }
}
