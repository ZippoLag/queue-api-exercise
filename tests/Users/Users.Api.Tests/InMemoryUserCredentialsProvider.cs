using QueueApi.Auth;

namespace Users.Api.Tests;

/// <summary>
/// A fixed set of known users used by integration tests.
/// </summary>
/// <remarks>
/// Lets tests prove regular-user semantics with a user that is not in the seeded store (spec
/// "every other authenticated user SHALL be treated as a regular user") without touching the database.
/// </remarks>
public class InMemoryUserCredentialsProvider : IUserCredentialsProvider
{
    private readonly IReadOnlyDictionary<string, string> _passwords;

    /// <summary>
    /// Creates the provider from a list of known username/password pairs.
    /// </summary>
    /// <param name="users">The known users as <c>(Username, Password)</c> tuples.</param>
    public InMemoryUserCredentialsProvider(params (string Username, string Password)[] users)
    {
        _passwords = users.ToDictionary(user => user.Username, user => user.Password, StringComparer.Ordinal);
    }

    /// <inheritdoc/>
    public Task<bool> VerifyCredentialsAsync(string username, string password)
        => Task.FromResult(_passwords.TryGetValue(username, out var expectedPassword)
            && string.Equals(expectedPassword, password, StringComparison.Ordinal));

    /// <inheritdoc/>
    public Task<bool> UserExistsAsync(string username)
        => Task.FromResult(_passwords.ContainsKey(username));
}
