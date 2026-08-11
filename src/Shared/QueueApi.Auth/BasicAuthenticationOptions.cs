using Microsoft.AspNetCore.Authentication;

namespace QueueApi.Auth;

/// <summary>
/// Options for the Basic authentication scheme implemented by <see cref="BasicAuthenticationHandler"/>.
/// </summary>
/// <remarks>
/// Carries only presentation-level settings; user credentials are resolved at runtime through
/// <see cref="IUserCredentialsProvider"/>, keeping the handler generic and reusable across APIs
/// (architecture: both APIs share the same Basic Auth implementation).
/// </remarks>
public class BasicAuthenticationOptions : AuthenticationSchemeOptions
{
    /// <summary>
    /// The realm advertised in the <c>WWW-Authenticate: Basic</c> challenge header.
    /// </summary>
    /// <value>Defaults to <c>"QueueApi"</c>; must not be empty for the challenge header to be valid.</value>
    public string Realm { get; set; } = "QueueApi";
}
