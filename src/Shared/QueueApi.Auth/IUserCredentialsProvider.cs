namespace QueueApi.Auth;

/// <summary>
/// Verifies credentials for Basic authentication against a user store.
/// </summary>
/// <remarks>
/// This seam decouples the authentication handler from the credential source (a database-backed
/// store today, via <see cref="DbUserCredentialsProvider"/>; in-memory dictionaries in tests) and
/// lets the 403 path be exercised in tests by registering an extra user (design decision D3).
/// Verification is the only credential operation exposed: with hashed storage there is no
/// "password" to return, and a wrong password is indistinguishable from an unknown user
/// (both yield <see langword="false"/>), which keeps the externally observable <c>401</c> behavior.
/// </remarks>
public interface IUserCredentialsProvider
{
    /// <summary>
    /// Verifies whether <paramref name="password"/> is the valid password for <paramref name="username"/>.
    /// </summary>
    /// <param name="username">The username presented in the <c>Authorization</c> header.</param>
    /// <param name="password">The password presented in the <c>Authorization</c> header.</param>
    /// <returns>
    /// <see langword="true"/> when the credentials are valid; <see langword="false"/> for an unknown
    /// username or a wrong password, which the caller treats identically.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    /// The credential store could not be accessed (unreachable or uninitialized);
    /// <see cref="DbUserCredentialsProvider"/> wraps the failure with setup guidance.
    /// </exception>
    Task<bool> VerifyCredentialsAsync(string username, string password);

    /// <summary>
    /// Checks whether <paramref name="username"/> exists in the credential store.
    /// </summary>
    /// <param name="username">The username to look up.</param>
    /// <returns><see langword="true"/> when the user exists, <see langword="false"/> otherwise.</returns>
    /// <exception cref="InvalidOperationException">
    /// The credential store could not be accessed (unreachable or uninitialized);
    /// <see cref="DbUserCredentialsProvider"/> wraps the failure with setup guidance.
    /// </exception>
    /// <remarks>
    /// Used by the host's startup check to fail fast when the store has not been initialized with
    /// the reserved cms user (spec "Credentials are sourced from the credential store").
    /// </remarks>
    Task<bool> UserExistsAsync(string username);
}
