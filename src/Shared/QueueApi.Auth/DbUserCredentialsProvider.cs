using System.Data.Common;
using Microsoft.EntityFrameworkCore;

namespace QueueApi.Auth;

/// <summary>
/// Verifies credentials against the shared database-backed credential store.
/// </summary>
/// <remarks>
/// Spec "Credentials are sourced from the credential store": the store is the source of truth for
/// credentials. Spec "Passwords are verified against stored hashes": the presented password is
/// re-derived with the stored salt and compared in constant time by
/// <see cref="Pbkdf2PasswordHasher.Verify(string, string)"/>. An unreachable or uninitialized store
/// surfaces as an <see cref="InvalidOperationException"/> with setup guidance instead of a cryptic
/// SQL error (design decision D7). Failures are caught via <see cref="DbException"/>, the base class of
/// every ADO.NET provider's connection errors, so the wrapping stays correct when the SQLite provider
/// is swapped for another engine (design decision D1).
/// </remarks>
public class DbUserCredentialsProvider : IUserCredentialsProvider
{
    private readonly AuthDbContext _dbContext;

    /// <summary>
    /// Creates the provider backed by the given credential store context.
    /// </summary>
    /// <param name="dbContext">The context exposing the credential store's <c>Users</c> table.</param>
    public DbUserCredentialsProvider(AuthDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc/>
    public async Task<bool> VerifyCredentialsAsync(string username, string password)
    {
        var user = await FindUserAsync(username);
        if (user is null)
        {
            return false;
        }

        return Pbkdf2PasswordHasher.Verify(password, user.PasswordHash);
    }

    /// <inheritdoc/>
    public async Task<bool> UserExistsAsync(string username)
    {
        try
        {
            return await _dbContext.Users.AnyAsync(user => user.Username == username);
        }
        catch (DbException exception)
        {
            throw CreateStoreUnavailableException(exception);
        }
    }

    private async Task<UserCredential?> FindUserAsync(string username)
    {
        try
        {
            return await _dbContext.Users.SingleOrDefaultAsync(user => user.Username == username);
        }
        catch (DbException exception)
        {
            throw CreateStoreUnavailableException(exception);
        }
    }

    private static InvalidOperationException CreateStoreUnavailableException(DbException exception)
        => new(
            "The credential store could not be accessed. Make sure the database exists and has been "
            + "initialized by running 'scripts/init-db.sh' from the repository root.",
            exception);
}
