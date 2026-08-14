using System.Net;
using System.Net.Http.Headers;
using System.Text;
using CmsWebhook.Domain;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using QueueApi.Auth;

namespace CmsWebhook.Api.Tests;

/// <summary>
/// Integration tests for Basic authentication on the CMS Webhook API against a seeded SQLite store.
/// </summary>
public class CmsWebhookApiAuthTests
{
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
    public async Task PostEvents_WithoutAuthorizationHeader_ReturnsUnauthorized()
    {
        using var factory = new CmsWebhookApiFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsync("/cms/events", Json(ValidPublish()));

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
    public async Task PostEvents_WithBearerScheme_ReturnsUnauthorized()
    {
        using var factory = new CmsWebhookApiFactory();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "some-token");

        var response = await client.PostAsync("/cms/events", Json(ValidPublish()));

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
    public async Task PostEvents_WithMalformedBase64_ReturnsUnauthorized()
    {
        using var factory = new CmsWebhookApiFactory();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", "Basic !!!not-base64!!!");

        var response = await client.PostAsync("/cms/events", Json(ValidPublish()));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await response.Content.ReadAsStringAsync()).Should().BeEmpty();
    }

    /// <summary>
    /// Verifies a request with credentials of an unknown user is rejected with <c>401</c>.
    /// </summary>
    /// <remarks>
    /// Source business rule: spec "All endpoints require authentication", scenario
    /// "Request with credentials of an unknown user"; the seeded store only contains the cms user.
    /// </remarks>
    [Fact]
    public async Task PostEvents_WithUnknownUsername_ReturnsUnauthorized()
    {
        using var factory = new CmsWebhookApiFactory();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = Basic("no-such-user", CmsWebhookApiFactory.CmsPassword);

        var response = await client.PostAsync("/cms/events", Json(ValidPublish()));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await response.Content.ReadAsStringAsync()).Should().BeEmpty();
    }

    /// <summary>
    /// Verifies a request with a wrong password for a known user is rejected with <c>401</c>.
    /// </summary>
    /// <remarks>
    /// Source business rule: spec "All endpoints require authentication", scenario
    /// "Request with a wrong password for a known user"; the password is verified against the
    /// stored PBKDF2 hash (spec "Passwords are verified against stored hashes").
    /// </remarks>
    [Fact]
    public async Task PostEvents_WithWrongPassword_ReturnsUnauthorized()
    {
        using var factory = new CmsWebhookApiFactory();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = Basic(CmsWebhookApiFactory.CmsUsername, "wrong-password");

        var response = await client.PostAsync("/cms/events", Json(ValidPublish()));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await response.Content.ReadAsStringAsync()).Should().BeEmpty();
    }

    /// <summary>
    /// Verifies a request with valid cms credentials succeeds and the event is eventually processed.
    /// </summary>
    /// <remarks>
    /// Source business rule: spec "Only the cms user is authorized", scenario
    /// "Valid credentials for the cms user"; spec "Credentials are sourced from the credential store",
    /// scenario "Credential store is initialized". The event is awaited to
    /// <see cref="CmsEventStatus.Processed"/> so the worker finishes before the factory is disposed —
    /// without the wait, teardown can cancel a mid-processing worker and the CI coverage gate flaps
    /// between runs.
    /// </remarks>
    [Fact]
    public async Task PostEvents_WithValidCmsCredentials_ReturnsCreated()
    {
        using var factory = new CmsWebhookApiFactory();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = Basic(CmsWebhookApiFactory.CmsUsername, CmsWebhookApiFactory.CmsPassword);

        var response = await client.PostAsync("/cms/events", Json(ValidPublish()));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        await WaitForEventProcessedAsync(factory, "entity-1");
    }

    /// <summary>
    /// Verifies a request with valid credentials of a non-cms user is rejected with <c>403</c>.
    /// </summary>
    /// <remarks>
    /// Source business rule: spec "Only the cms user is authorized", scenario
    /// "Valid credentials for a non-cms user"; the second user is injected through the provider seam.
    /// </remarks>
    [Fact]
    public async Task PostEvents_WithValidNonCmsCredentials_ReturnsForbidden()
    {
        var provider = new InMemoryUserCredentialsProvider(
            (CmsWebhookApiFactory.CmsUsername, CmsWebhookApiFactory.CmsPassword),
            (OtherUsername, OtherPassword));
        using var factory = new CmsWebhookApiFactory(credentialsProviderOverride: provider);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = Basic(OtherUsername, OtherPassword);

        var response = await client.PostAsync("/cms/events", Json(ValidPublish()));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await response.Content.ReadAsStringAsync()).Should().BeEmpty();
    }

    /// <summary>
    /// Verifies the host fails to start when the credential store has not been initialized with the schema.
    /// </summary>
    /// <remarks>
    /// Source business rule: spec "Credentials are sourced from the credential store", scenario
    /// "Credential store is not initialized"; the provider wraps the missing-table failure with setup guidance.
    /// </remarks>
    [Fact]
    public void CreateClient_WhenStoreIsNotInitialized_ThrowsWithGuidance()
    {
        var emptyDatabasePath = Path.GetTempFileName();
        try
        {
            using var factory = new CmsWebhookApiFactory(authDbConnectionString: $"Data Source={emptyDatabasePath}");

            var exception = CaptureStartupFailure(factory);

            exception.Message.Should().Contain("scripts/init-db.sh");
        }
        finally
        {
            File.Delete(emptyDatabasePath);
        }
    }

    /// <summary>
    /// Verifies the host fails to start when the store is initialized but lacks the cms user.
    /// </summary>
    /// <remarks>
    /// Source business rule: spec "Credentials are sourced from the credential store", scenario
    /// "Credential store is not initialized"; an initialized store without the cms user is equally unusable.
    /// </remarks>
    [Fact]
    public void CreateClient_WhenStoreLacksCmsUser_ThrowsWithGuidance()
    {
        var databasePath = CreateInitializedStoreWithoutUsers();
        try
        {
            using var factory = new CmsWebhookApiFactory(authDbConnectionString: $"Data Source={databasePath}");

            var exception = CaptureStartupFailure(factory);

            exception.Message.Should().Contain("does not contain the cms user");
            exception.Message.Should().Contain("scripts/init-db.sh");
        }
        finally
        {
            File.Delete(databasePath);
        }
    }

    /// <summary>
    /// Verifies the host fails to start when the configured cms username violates the <c>[10,20]</c> length rule.
    /// </summary>
    /// <remarks>
    /// Source business rule: spec "Configured credential format", scenario "Invalid configured username
    /// length"; architecture: <c>username [10,20]</c> characters. The invalid username is injected via the
    /// <c>Auth__CmsUsername</c> configuration environment variable: process environment variables are part of
    /// the default configuration chain read during the host build, so the value is visible to the top-level
    /// <c>Program.cs</c> config reads (design decision D2 of change remove-legacy-auth-env).
    /// </remarks>
    [Fact]
    public void CreateClient_WhenConfiguredUsernameLengthIsInvalid_ThrowsAtStartup()
    {
        var previousValue = Environment.GetEnvironmentVariable("Auth__CmsUsername");
        Environment.SetEnvironmentVariable("Auth__CmsUsername", "123456789");
        try
        {
            using var factory = new CmsWebhookApiFactory();

            var exception = CaptureStartupFailure(factory);

            exception.Message.Should().Contain("between 10 and 20");
        }
        finally
        {
            Environment.SetEnvironmentVariable("Auth__CmsUsername", previousValue);
        }
    }

    private static InvalidOperationException CaptureStartupFailure(CmsWebhookApiFactory factory)
    {
        var exception = Record.Exception(() => factory.CreateClient());
        var invalidOperation = exception as InvalidOperationException
            ?? exception?.InnerException as InvalidOperationException;
        invalidOperation.Should().NotBeNull($"expected an InvalidOperationException, but got '{exception}'");
        return invalidOperation!;
    }

    private static string CreateInitializedStoreWithoutUsers()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"queue-api-auth-tests-empty-{Guid.NewGuid():N}.db");
        var options = new DbContextOptionsBuilder<AuthDbContext>()
            .UseSqlite($"Data Source={databasePath}")
            .Options;
        using var context = new AuthDbContext(options);
        context.Database.EnsureCreated();
        return databasePath;
    }

    /// <summary>
    /// Waits until the recorded event for the given entity id is processed by the outbox worker.
    /// </summary>
    /// <param name="factory">The test host whose outbox worker processes the event.</param>
    /// <param name="entityId">The entity id the event refers to.</param>
    /// <returns>The processed event.</returns>
    /// <exception cref="Xunit.Sdk.XunitException">The event was not processed within the timeout.</exception>
    private static async Task<CmsEvent> WaitForEventProcessedAsync(CmsWebhookApiFactory factory, string entityId)
    {
        using var context = factory.CreateCmsDbContext();
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (DateTime.UtcNow < deadline)
        {
            // AsNoTracking: the same context is polled repeatedly and a tracked entity would keep the
            // stale in-memory status; re-reading without tracking sees the worker's update.
            var @event = await context.Events.AsNoTracking().SingleOrDefaultAsync(item => item.EntityId == entityId);
            if (@event is not null && @event.Status == CmsEventStatus.Processed)
            {
                return @event;
            }

            await Task.Delay(100);
        }

        throw new Xunit.Sdk.XunitException($"Event for entity '{entityId}' was not processed within the timeout.");
    }

    private static AuthenticationHeaderValue Basic(string username, string password)
        => new("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes($"{username}:{password}")));

    private static StringContent Json(string body) => new(body, Encoding.UTF8, "application/json");

    private static string ValidPublish(string id = "entity-1")
        => $$"""{"type":"publish","id":"{{id}}","payload":{"title":"hello"},"version":1,"timestamp":"2024-01-01T00:00:00Z"}""";
}
