using System.Net;
using System.Net.Http.Headers;
using System.Text;
using FluentAssertions;
using QueueApi.Auth;

namespace CmsWebhook.Api.Tests;

/// <summary>
/// Integration tests for Basic authentication on the CMS Webhook API.
/// </summary>
public class CmsWebhookApiAuthTests
{
    private const string CmsUsername = "cms-webhook";
    private const string CmsPassword = "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa";
    private const string OtherUsername = "other-client";
    private const string OtherPassword = "bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb";

    /// <summary>
    /// Verifies a request without an <c>Authorization</c> header is rejected with <c>401</c>.
    /// </summary>
    /// <remarks>
    /// Source business rule: spec "All endpoints require authentication", scenario
    /// "Request without Authorization header".
    /// </remarks>
    [Fact]
    public async Task GetRoot_WithoutAuthorizationHeader_ReturnsUnauthorized()
    {
        using var environment = SetValidCmsEnvironment();
        using var factory = new CmsWebhookApiFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await response.Content.ReadAsStringAsync()).Should().BeEmpty();
        response.Headers.WwwAuthenticate.Should().ContainSingle();
        response.Headers.WwwAuthenticate.Single().Scheme.Should().Be("Basic");
    }

    /// <summary>
    /// Verifies a request using an unsupported authorization scheme is rejected with <c>401</c>.
    /// </summary>
    /// <remarks>
    /// Source business rule: spec "All endpoints require authentication", scenario
    /// "Request with an unsupported authorization scheme".
    /// </remarks>
    [Fact]
    public async Task GetRoot_WithBearerScheme_ReturnsUnauthorized()
    {
        using var environment = SetValidCmsEnvironment();
        using var factory = new CmsWebhookApiFactory();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "some-token");

        var response = await client.GetAsync("/");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await response.Content.ReadAsStringAsync()).Should().BeEmpty();
    }

    /// <summary>
    /// Verifies a request with malformed base64 Basic credentials is rejected with <c>401</c>.
    /// </summary>
    /// <remarks>
    /// Source business rule: spec "All endpoints require authentication", scenario
    /// "Request with malformed Basic credentials".
    /// </remarks>
    [Fact]
    public async Task GetRoot_WithMalformedBase64_ReturnsUnauthorized()
    {
        using var environment = SetValidCmsEnvironment();
        using var factory = new CmsWebhookApiFactory();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", "Basic !!!not-base64!!!");

        var response = await client.GetAsync("/");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await response.Content.ReadAsStringAsync()).Should().BeEmpty();
    }

    /// <summary>
    /// Verifies a request with credentials of an unknown user is rejected with <c>401</c>.
    /// </summary>
    /// <remarks>
    /// Source business rule: spec "All endpoints require authentication", scenario
    /// "Request with credentials of an unknown user".
    /// </remarks>
    [Fact]
    public async Task GetRoot_WithUnknownUsername_ReturnsUnauthorized()
    {
        using var environment = SetValidCmsEnvironment();
        using var factory = new CmsWebhookApiFactory();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = Basic("no-such-user", CmsPassword);

        var response = await client.GetAsync("/");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await response.Content.ReadAsStringAsync()).Should().BeEmpty();
    }

    /// <summary>
    /// Verifies a request with a wrong password for a known user is rejected with <c>401</c>.
    /// </summary>
    /// <remarks>
    /// Source business rule: spec "All endpoints require authentication", scenario
    /// "Request with a wrong password for a known user".
    /// </remarks>
    [Fact]
    public async Task GetRoot_WithWrongPassword_ReturnsUnauthorized()
    {
        using var environment = SetValidCmsEnvironment();
        using var factory = new CmsWebhookApiFactory();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = Basic(CmsUsername, "wrong-password");

        var response = await client.GetAsync("/");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await response.Content.ReadAsStringAsync()).Should().BeEmpty();
    }

    /// <summary>
    /// Verifies a request with valid cms credentials succeeds.
    /// </summary>
    /// <remarks>
    /// Source business rule: spec "Only the cms user is authorized", scenario
    /// "Valid credentials for the cms user".
    /// </remarks>
    [Fact]
    public async Task GetRoot_WithValidCmsCredentials_ReturnsOk()
    {
        using var environment = SetValidCmsEnvironment();
        using var factory = new CmsWebhookApiFactory();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = Basic(CmsUsername, CmsPassword);

        var response = await client.GetAsync("/");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).Should().Be("Hello World!");
    }

    /// <summary>
    /// Verifies a request with valid credentials of a non-cms user is rejected with <c>403</c>.
    /// </summary>
    /// <remarks>
    /// Source business rule: spec "Only the cms user is authorized", scenario
    /// "Valid credentials for a non-cms user"; the second user is injected through the provider seam.
    /// </remarks>
    [Fact]
    public async Task GetRoot_WithValidNonCmsCredentials_ReturnsForbidden()
    {
        using var environment = SetValidCmsEnvironment();
        var provider = new InMemoryUserCredentialsProvider(
            (CmsUsername, CmsPassword),
            (OtherUsername, OtherPassword));
        using var factory = new CmsWebhookApiFactory(provider);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = Basic(OtherUsername, OtherPassword);

        var response = await client.GetAsync("/");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await response.Content.ReadAsStringAsync()).Should().BeEmpty();
    }

    /// <summary>
    /// Verifies the host fails to start when the required environment variables are not set.
    /// </summary>
    /// <remarks>
    /// Source business rule: spec "Credentials are sourced from environment variables", scenario
    /// "Environment variables are missing".
    /// </remarks>
    [Fact]
    public void CreateClient_WhenCredentialsAreMissing_ThrowsAtStartup()
    {
        using var environment = new EnvironmentVariableScope();
        environment.Set(EnvironmentUserCredentialsProvider.UsernameEnvironmentVariable, null);
        environment.Set(EnvironmentUserCredentialsProvider.PasswordEnvironmentVariable, null);
        using var factory = new CmsWebhookApiFactory();

        var exception = CaptureStartupFailure(factory);

        exception.Message.Should().Contain(EnvironmentUserCredentialsProvider.UsernameEnvironmentVariable);
    }

    /// <summary>
    /// Verifies the host fails to start when the configured username violates the <c>[10,20]</c> length rule.
    /// </summary>
    /// <remarks>
    /// Source business rule: spec "Configured credential format", scenario
    /// "Invalid configured username length"; architecture: <c>username [10,20]</c>.
    /// </remarks>
    [Fact]
    public void CreateClient_WhenConfiguredUsernameLengthIsInvalid_ThrowsAtStartup()
    {
        using var environment = new EnvironmentVariableScope();
        environment.Set(EnvironmentUserCredentialsProvider.UsernameEnvironmentVariable, "123456789");
        environment.Set(EnvironmentUserCredentialsProvider.PasswordEnvironmentVariable, CmsPassword);
        using var factory = new CmsWebhookApiFactory();

        var exception = CaptureStartupFailure(factory);

        exception.Message.Should().Contain("between 10 and 20");
    }

    private static InvalidOperationException CaptureStartupFailure(CmsWebhookApiFactory factory)
    {
        var exception = Record.Exception(() => factory.CreateClient());
        var invalidOperation = exception as InvalidOperationException
            ?? exception?.InnerException as InvalidOperationException;
        invalidOperation.Should().NotBeNull($"expected an InvalidOperationException, but got '{exception}'");
        return invalidOperation!;
    }

    private static EnvironmentVariableScope SetValidCmsEnvironment()
    {
        var scope = new EnvironmentVariableScope();
        scope.Set(EnvironmentUserCredentialsProvider.UsernameEnvironmentVariable, CmsUsername);
        scope.Set(EnvironmentUserCredentialsProvider.PasswordEnvironmentVariable, CmsPassword);
        return scope;
    }

    private static AuthenticationHeaderValue Basic(string username, string password)
        => new("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes($"{username}:{password}")));

    private sealed class EnvironmentVariableScope : IDisposable
    {
        private readonly Dictionary<string, string?> _previousValues = new();

        public void Set(string name, string? value)
        {
            _previousValues[name] = Environment.GetEnvironmentVariable(name);
            Environment.SetEnvironmentVariable(name, value);
        }

        public void Dispose()
        {
            foreach (var (name, value) in _previousValues)
            {
                Environment.SetEnvironmentVariable(name, value);
            }
        }
    }
}
