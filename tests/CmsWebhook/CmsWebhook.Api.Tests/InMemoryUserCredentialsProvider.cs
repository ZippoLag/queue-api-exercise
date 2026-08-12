using QueueApi.Auth;

namespace CmsWebhook.Api.Tests;

/// <summary>
/// A fixed set of known users used by integration tests.
/// </summary>
/// <remarks>
/// Lets tests prove the 403 path with a second valid user (spec "Only the cms user is authorized",
/// scenario "Valid credentials for a non-cms user") without touching the database.
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
