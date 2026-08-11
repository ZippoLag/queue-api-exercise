using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using QueueApi.Auth;

namespace QueueApi.Auth.Tests;

/// <summary>
/// Unit tests for <see cref="BasicAuthenticationHandler"/>'s <c>HandleAuthenticateAsync</c> and challenge flow.
/// </summary>
public class BasicAuthenticationHandlerTests
{
    private const string CmsUsername = "cms-webhook";
    private const string CmsPassword = "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa";

    private static readonly AuthenticationScheme Scheme = new(
        BasicAuthenticationDefaults.AuthenticationScheme,
        displayName: null,
        handlerType: typeof(BasicAuthenticationHandler));

    /// <summary>
    /// Verifies a request without an <c>Authorization</c> header yields no authentication result.
    /// </summary>
    /// <remarks>
    /// Source business rule: spec "All endpoints require authentication", scenario
    /// "Request without Authorization header"; a <c>NoResult</c> makes the authorization middleware
    /// issue a <c>401</c> challenge.
    /// </remarks>
    [Fact]
    public async Task AuthenticateAsync_WithoutAuthorizationHeader_ReturnsNoResult()
    {
        var handler = CreateHandler();
        var context = new DefaultHttpContext();

        await handler.InitializeAsync(Scheme, context);
        var result = await handler.AuthenticateAsync();

        result.None.Should().BeTrue();
    }

    /// <summary>
    /// Verifies a non-<c>Basic</c> authorization scheme is rejected.
    /// </summary>
    /// <remarks>
    /// Source business rule: spec "All endpoints require authentication", scenario
    /// "Request with an unsupported authorization scheme".
    /// </remarks>
    [Fact]
    public async Task AuthenticateAsync_WithUnsupportedScheme_ReturnsFail()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers.Authorization = "Bearer some-token";

        var handler = CreateHandler();
        await handler.InitializeAsync(Scheme, context);
        var result = await handler.AuthenticateAsync();

        result.Succeeded.Should().BeFalse();
        result.Failure.Should().NotBeNull();
    }

    /// <summary>
    /// Verifies credentials that cannot be base64-decoded are rejected.
    /// </summary>
    /// <remarks>
    /// Source business rule: spec "All endpoints require authentication", scenario
    /// "Request with malformed Basic credentials".
    /// </remarks>
    [Fact]
    public async Task AuthenticateAsync_WithMalformedBase64_ReturnsFail()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers.Authorization = "Basic !!!not-base64!!!";

        var handler = CreateHandler();
        await handler.InitializeAsync(Scheme, context);
        var result = await handler.AuthenticateAsync();

        result.Succeeded.Should().BeFalse();
        result.Failure.Should().NotBeNull();
    }

    /// <summary>
    /// Verifies Basic credentials without a <c>username:password</c> separator are rejected.
    /// </summary>
    /// <remarks>
    /// Source business rule: spec "All endpoints require authentication", scenario
    /// "Request with malformed Basic credentials".
    /// </remarks>
    [Fact]
    public async Task AuthenticateAsync_WithCredentialsMissingSeparator_ReturnsFail()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers.Authorization = Basic(CmsUsername);

        var handler = CreateHandler();
        await handler.InitializeAsync(Scheme, context);
        var result = await handler.AuthenticateAsync();

        result.Succeeded.Should().BeFalse();
        result.Failure.Should().NotBeNull();
    }

    /// <summary>
    /// Verifies credentials with an unknown username are rejected.
    /// </summary>
    /// <remarks>
    /// Source business rule: spec "All endpoints require authentication", scenario
    /// "Request with credentials of an unknown user".
    /// </remarks>
    [Fact]
    public async Task AuthenticateAsync_WithUnknownUsername_ReturnsFail()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers.Authorization = Basic("unknown-user", CmsPassword);

        var provider = new Mock<IUserCredentialsProvider>();
        provider.Setup(p => p.GetPassword(It.IsAny<string>())).Returns((string?)null);

        var handler = CreateHandler(provider.Object);
        await handler.InitializeAsync(Scheme, context);
        var result = await handler.AuthenticateAsync();

        result.Succeeded.Should().BeFalse();
        result.Failure.Should().NotBeNull();
    }

    /// <summary>
    /// Verifies a wrong password for a known username is rejected.
    /// </summary>
    /// <remarks>
    /// Source business rule: spec "All endpoints require authentication", scenario
    /// "Request with a wrong password for a known user".
    /// </remarks>
    [Fact]
    public async Task AuthenticateAsync_WithWrongPassword_ReturnsFail()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers.Authorization = Basic(CmsUsername, "wrong-password");

        var provider = new Mock<IUserCredentialsProvider>();
        provider.Setup(p => p.GetPassword(CmsUsername)).Returns(CmsPassword);

        var handler = CreateHandler(provider.Object);
        await handler.InitializeAsync(Scheme, context);
        var result = await handler.AuthenticateAsync();

        result.Succeeded.Should().BeFalse();
        result.Failure.Should().NotBeNull();
    }

    /// <summary>
    /// Verifies a lowercase <c>basic</c> scheme prefix is accepted, matching RFC 7235 scheme case-insensitivity.
    /// </summary>
    /// <remarks>
    /// Source business rule: spec "All endpoints require authentication", scenario "Valid credentials for the
    /// cms user"; HTTP authentication schemes are case-insensitive, so <c>Basic</c> and <c>basic</c> are equivalent.
    /// </remarks>
    [Fact]
    public async Task AuthenticateAsync_WithLowercaseBasicScheme_ReturnsSuccess()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers.Authorization = "basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes($"{CmsUsername}:{CmsPassword}"));

        var provider = new Mock<IUserCredentialsProvider>();
        provider.Setup(p => p.GetPassword(CmsUsername)).Returns(CmsPassword);

        var handler = CreateHandler(provider.Object);
        await handler.InitializeAsync(Scheme, context);
        var result = await handler.AuthenticateAsync();

        result.Succeeded.Should().BeTrue();
    }

    /// <summary>
    /// Verifies valid cms credentials succeed and produce a principal carrying the username claim and no role claim.
    /// </summary>
    /// <remarks>
    /// Source business rule: spec "Only the cms user is authorized", scenario "Valid credentials for the
    /// cms user"; the username claim is what the authorization policy matches against. The principal must
    /// carry no <c>ClaimTypes.Role</c> claim because nothing consumes one and a dead role would imply
    /// authorization semantics that don't exist (design decision 2).
    /// </remarks>
    [Fact]
    public async Task AuthenticateAsync_WithValidCmsCredentials_ReturnsSuccessWithUsernameClaim()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers.Authorization = Basic(CmsUsername, CmsPassword);

        var provider = new Mock<IUserCredentialsProvider>();
        provider.Setup(p => p.GetPassword(CmsUsername)).Returns(CmsPassword);

        var handler = CreateHandler(provider.Object);
        await handler.InitializeAsync(Scheme, context);
        var result = await handler.AuthenticateAsync();

        result.Succeeded.Should().BeTrue();
        result.Principal!.Identity!.Name.Should().Be(CmsUsername);
        result.Principal.HasClaim(ClaimTypes.Name, CmsUsername).Should().BeTrue();
        result.Principal.HasClaim(ClaimTypes.Role, "AuthenticatedUser").Should().BeFalse();
        result.Principal.Claims.Should().NotContain(c => c.Type == ClaimTypes.Role);
        result.Ticket!.AuthenticationScheme.Should().Be(BasicAuthenticationDefaults.AuthenticationScheme);
    }

    /// <summary>
    /// Verifies the challenge sets <c>401</c> with a <c>WWW-Authenticate: Basic</c> header.
    /// </summary>
    /// <remarks>
    /// Source business rule: spec "All endpoints require authentication"; the challenge advertises the
    /// <c>Basic</c> scheme and realm so clients can retry with credentials (code review task: HTTP surface).
    /// </remarks>
    [Fact]
    public async Task ChallengeAsync_SetsUnauthorizedStatusAndWwwAuthenticateHeader()
    {
        var handler = CreateHandler();
        var context = new DefaultHttpContext();

        await handler.InitializeAsync(Scheme, context);
        await handler.ChallengeAsync(new AuthenticationProperties());

        context.Response.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
        context.Response.Headers.WWWAuthenticate.Should().Equal("Basic realm=\"QueueApi\"");
    }

    private static BasicAuthenticationHandler CreateHandler(IUserCredentialsProvider? provider = null)
    {
        provider ??= Mock.Of<IUserCredentialsProvider>();
        var options = new BasicAuthenticationOptions();
        var optionsMonitor = new Mock<IOptionsMonitor<BasicAuthenticationOptions>>();
        optionsMonitor.SetupGet(m => m.CurrentValue).Returns(options);
        optionsMonitor.Setup(m => m.Get(It.IsAny<string>())).Returns(options);
        return new BasicAuthenticationHandler(optionsMonitor.Object, NullLoggerFactory.Instance, UrlEncoder.Default, provider);
    }

    private static string Basic(string username, string password)
        => "Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes($"{username}:{password}"));

    private static string Basic(string usernameOnly)
        => "Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes(usernameOnly));
}
