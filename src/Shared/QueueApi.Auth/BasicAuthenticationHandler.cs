using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace QueueApi.Auth;

/// <summary>
/// Authenticates requests that present HTTP Basic credentials.
/// </summary>
/// <remarks>
/// Spec "All endpoints require authentication": a missing <c>Authorization</c> header, an unsupported scheme,
/// undecodable credentials, an unknown username or a wrong password produce a failed result that the
/// authorization middleware turns into <c>401</c>, and the challenge advertises the <c>Basic</c> scheme so
/// clients know how to authenticate. Passwords are compared in constant time (design decision 6) so the
/// handler does not leak information about the stored password.
/// </remarks>
public class BasicAuthenticationHandler : AuthenticationHandler<BasicAuthenticationOptions>
{
    private const string BasicSchemePrefix = "Basic ";
    private readonly IUserCredentialsProvider _credentialsProvider;

    /// <summary>
    /// Creates the handler with the given options monitor, logger factory, URL encoder and credential provider.
    /// </summary>
    /// <param name="options">The options monitor providing the scheme's <see cref="BasicAuthenticationOptions"/>.</param>
    /// <param name="logger">The logger factory used to obtain the handler's logger.</param>
    /// <param name="encoder">The URL encoder used by the base class.</param>
    /// <param name="credentialsProvider">The provider that resolves usernames to passwords.</param>
    public BasicAuthenticationHandler(
        IOptionsMonitor<BasicAuthenticationOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        IUserCredentialsProvider credentialsProvider)
        : base(options, logger, encoder)
    {
        _credentialsProvider = credentialsProvider;
    }

    /// <inheritdoc/>
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue("Authorization", out var authorizationHeader)
            || string.IsNullOrWhiteSpace(authorizationHeader))
        {
            Logger.LogWarning("Basic authentication rejected: missing Authorization header.");
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var headerValue = authorizationHeader.ToString();
        if (!headerValue.StartsWith(BasicSchemePrefix, StringComparison.OrdinalIgnoreCase))
        {
            Logger.LogWarning("Basic authentication rejected: unsupported authorization scheme.");
            return Task.FromResult(AuthenticateResult.Fail("Unsupported authorization scheme."));
        }

        var decodedCredentials = DecodeCredentials(headerValue[BasicSchemePrefix.Length..].Trim());
        if (decodedCredentials is null)
        {
            Logger.LogWarning("Basic authentication rejected: malformed Basic credentials.");
            return Task.FromResult(AuthenticateResult.Fail("Malformed Basic credentials."));
        }

        var separatorIndex = decodedCredentials.IndexOf(':');
        if (separatorIndex < 0)
        {
            Logger.LogWarning("Basic authentication rejected: malformed Basic credentials.");
            return Task.FromResult(AuthenticateResult.Fail("Malformed Basic credentials."));
        }

        var username = decodedCredentials[..separatorIndex];
        var password = decodedCredentials[(separatorIndex + 1)..];

        var expectedPassword = _credentialsProvider.GetPassword(username);
        if (expectedPassword is null)
        {
            Logger.LogWarning("Basic authentication rejected: unknown username '{Username}'.", username);
            return Task.FromResult(AuthenticateResult.Fail("Unknown username."));
        }

        var providedBytes = Encoding.UTF8.GetBytes(password);
        var expectedBytes = Encoding.UTF8.GetBytes(expectedPassword);
        if (providedBytes.Length != expectedBytes.Length
            || !CryptographicOperations.FixedTimeEquals(providedBytes, expectedBytes))
        {
            Logger.LogWarning("Basic authentication rejected: invalid password for username '{Username}'.", username);
            return Task.FromResult(AuthenticateResult.Fail("Invalid password."));
        }

        var claims = new[]
        {
            new Claim(ClaimTypes.Name, username),
        };
        var identity = new ClaimsIdentity(claims, Scheme.Name);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, Scheme.Name);

        Logger.LogInformation("Basic authentication succeeded for username '{Username}'.", username);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }

    /// <inheritdoc/>
    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = StatusCodes.Status401Unauthorized;
        Response.Headers.WWWAuthenticate = $"Basic realm=\"{Options.Realm}\"";
        return Task.CompletedTask;
    }

    private static string? DecodeCredentials(string base64Value)
    {
        try
        {
            return Encoding.UTF8.GetString(Convert.FromBase64String(base64Value));
        }
        catch (FormatException)
        {
            return null;
        }
    }
}
