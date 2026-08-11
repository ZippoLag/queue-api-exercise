namespace QueueApi.Auth;

/// <summary>
/// Default values shared by the Basic authentication components of <c>QueueApi.Auth</c>.
/// </summary>
public static class BasicAuthenticationDefaults
{
    /// <summary>
    /// The name of the Basic authentication scheme registered by the <c>QueueApi.Auth</c> DI extension.
    /// </summary>
    /// <remarks>
    /// The scheme name is arbitrary as long as it is used consistently by the scheme registration,
    /// the authentication ticket and the challenge/forbid flow (design decision: "Scheme/option shape").
    /// </remarks>
    public const string AuthenticationScheme = "BasicAuth";
}
