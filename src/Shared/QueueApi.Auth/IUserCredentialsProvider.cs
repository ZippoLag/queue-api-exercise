namespace QueueApi.Auth;

/// <summary>
/// Resolves a username to its password for Basic authentication.
/// </summary>
/// <remarks>
/// This seam decouples the authentication handler from the credential source (environment variables today,
/// a persistence layer later) and lets the 403 path be exercised in tests by registering an extra user
/// (design decision: "Credential-provider abstraction enables unit/integration testing").
/// </remarks>
public interface IUserCredentialsProvider
{
    /// <summary>
    /// Gets the password for <paramref name="username"/>, or <see langword="null"/> when the user is unknown.
    /// </summary>
    /// <param name="username">The username presented in the <c>Authorization</c> header.</param>
    /// <returns>The matching password, or <see langword="null"/> when no such user is configured.</returns>
    string? GetPassword(string username);
}
