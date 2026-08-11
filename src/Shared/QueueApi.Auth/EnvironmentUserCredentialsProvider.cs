namespace QueueApi.Auth;

/// <summary>
/// Reads the CMS API's Basic authentication credentials exclusively from environment variables.
/// </summary>
/// <remarks>
/// Spec "Credentials are sourced from environment variables": <c>AUTH_CMS_USERNAME</c> and
/// <c>AUTH_CMS_PASSWORD</c> are read only from the environment, and a missing variable or a username
/// outside the architecture's <c>[10,20]</c> length rule throws an <see cref="InvalidOperationException"/>
/// so the application fails fast at startup instead of misbehaving at runtime.
/// </remarks>
public class EnvironmentUserCredentialsProvider : IUserCredentialsProvider
{
    /// <summary>
    /// The environment variable holding the CMS user's username.
    /// </summary>
    /// <remarks>The <c>AUTH_</c> prefix avoids collisions with unrelated configuration (design decision 4).</remarks>
    public const string UsernameEnvironmentVariable = "AUTH_CMS_USERNAME";

    /// <summary>
    /// The environment variable holding the CMS user's password.
    /// </summary>
    /// <remarks>The <c>AUTH_</c> prefix avoids collisions with unrelated configuration (design decision 4).</remarks>
    public const string PasswordEnvironmentVariable = "AUTH_CMS_PASSWORD";

    /// <summary>
    /// The username reserved by the architecture for the CMS system when it connects to the CMS API.
    /// </summary>
    /// <remarks>
    /// Architecture note: <c>"cms"</c> is the only identity allowed to connect to the CMS API; the actual
    /// credential used at runtime is configured through <see cref="UsernameEnvironmentVariable"/> and must
    /// satisfy the <c>[10,20]</c> username length rule.
    /// </remarks>
    public const string ReservedCmsUsername = "cms";

    /// <summary>
    /// The minimum allowed length of a configured username (architecture: <c>username [10,20]</c>).
    /// </summary>
    public const int MinUsernameLength = 10;

    /// <summary>
    /// The maximum allowed length of a configured username (architecture: <c>username [10,20]</c>).
    /// </summary>
    public const int MaxUsernameLength = 20;

    private readonly string _username;
    private readonly string _password;

    /// <summary>
    /// Creates a provider that reads credentials from the process environment.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// When <see cref="UsernameEnvironmentVariable"/> or <see cref="PasswordEnvironmentVariable"/> is not set,
    /// or the configured username is outside <c>[10,20]</c> characters.
    /// </exception>
    public EnvironmentUserCredentialsProvider()
        : this(Environment.GetEnvironmentVariable)
    {
    }

    /// <summary>
    /// Creates a provider that reads credentials through <paramref name="getEnvironmentVariable"/>.
    /// </summary>
    /// <param name="getEnvironmentVariable">A function mapping a variable name to its value; lets tests inject variables without mutating process-wide state.</param>
    /// <exception cref="InvalidOperationException">
    /// Same conditions as <see cref="EnvironmentUserCredentialsProvider()"/>.
    /// </exception>
    public EnvironmentUserCredentialsProvider(Func<string, string?> getEnvironmentVariable)
    {
        _username = getEnvironmentVariable(UsernameEnvironmentVariable)
            ?? throw new InvalidOperationException(
                $"Missing required environment variable '{UsernameEnvironmentVariable}'. "
                + "Basic authentication credentials for the CMS API must be provided via environment variables.");
        _password = getEnvironmentVariable(PasswordEnvironmentVariable)
            ?? throw new InvalidOperationException(
                $"Missing required environment variable '{PasswordEnvironmentVariable}'. "
                + "Basic authentication credentials for the CMS API must be provided via environment variables.");
        if (_username.Length < MinUsernameLength || _username.Length > MaxUsernameLength)
        {
            throw new InvalidOperationException(
                $"Configured username '{_username}' is {_username.Length} characters long; "
                + $"it must be between {MinUsernameLength} and {MaxUsernameLength} characters (architecture: username [10,20]).");
        }
    }

    /// <summary>
    /// The configured CMS username, used by the CMS API's authorization policy.
    /// </summary>
    public string Username => _username;

    /// <inheritdoc/>
    public string? GetPassword(string username)
        => string.Equals(username, _username, StringComparison.Ordinal) ? _password : null;
}
