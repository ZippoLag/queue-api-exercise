using System.Net;
using System.Net.Http.Headers;
using System.Text;
using CmsWebhook.Domain;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace CmsWebhook.Api.Tests;

/// <summary>
/// Integration tests for the <c>POST /cms/events</c> endpoint: authentication, acceptance, validation,
/// batch atomicity, and asynchronous processing into the entity store.
/// </summary>
public class CmsWebhookApiEventIngestionTests
{
    private const string OtherUsername = "other-client";
    private const string OtherPassword = "bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb";

    /// <summary>
    /// Verifies the endpoint rejects unauthenticated requests.
    /// </summary>
    /// <remarks>Source business rule: spec "Endpoint requires authentication", scenario "Request without credentials".</remarks>
    [Fact]
    public async Task PostEvents_WithoutAuthorization_ReturnsUnauthorized()
    {
        using var factory = new CmsWebhookApiFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsync("/cms/events", Json(ValidPublish("entity-1")));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>
    /// Verifies the endpoint rejects valid credentials of a non-cms user.
    /// </summary>
    /// <remarks>Source business rule: spec "Endpoint requires authentication", scenario "Valid credentials of a non-cms user".</remarks>
    [Fact]
    public async Task PostEvents_WithValidNonCmsCredentials_ReturnsForbidden()
    {
        var provider = new InMemoryUserCredentialsProvider(
            (CmsWebhookApiFactory.CmsUsername, CmsWebhookApiFactory.CmsPassword),
            (OtherUsername, OtherPassword));
        using var factory = new CmsWebhookApiFactory(credentialsProviderOverride: provider);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = Basic(OtherUsername, OtherPassword);

        var response = await client.PostAsync("/cms/events", Json(ValidPublish("entity-1")));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    /// <summary>
    /// Verifies a valid single event is accepted and asynchronously processed into the entity store.
    /// </summary>
    /// <remarks>
    /// Source business rule: spec "Single event accepted", "Events are recorded before processing" and
    /// "Event is processed after acceptance".
    /// </remarks>
    [Fact]
    public async Task PostEvents_ValidSingleEvent_ReturnsCreatedAndIsProcessed()
    {
        using var factory = new CmsWebhookApiFactory();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = Basic(CmsWebhookApiFactory.CmsUsername, CmsWebhookApiFactory.CmsPassword);

        var response = await client.PostAsync("/cms/events", Json(ValidPublish("entity-1")));

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var entity = await WaitForEntityAsync(factory, "entity-1");
        entity.LatestVersion.Should().Be(1);
        entity.Payload.Should().Be("""{"title":"hello"}""");
        entity.IsPublished.Should().BeTrue();

        using var context = factory.CreateCmsDbContext();
        var @event = await context.Events.SingleAsync();
        @event.Status.Should().Be(CmsEventStatus.Processed);
    }

    /// <summary>
    /// Verifies a valid batch is accepted and every event is eventually processed.
    /// </summary>
    /// <remarks>Source business rule: spec "Batch of events accepted".</remarks>
    [Fact]
    public async Task PostEvents_ValidBatch_ReturnsCreatedAndProcessesAll()
    {
        using var factory = new CmsWebhookApiFactory();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = Basic(CmsWebhookApiFactory.CmsUsername, CmsWebhookApiFactory.CmsPassword);
        var batch = $"""
            [
              {ValidPublish("entity-1")},
              {ValidPublish("entity-2", version: 2)}
            ]
            """;

        var response = await client.PostAsync("/cms/events", Json(batch));

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var first = await WaitForEntityAsync(factory, "entity-1");
        first.LatestVersion.Should().Be(1);
        var second = await WaitForEntityAsync(factory, "entity-2");
        second.LatestVersion.Should().Be(2);

        using var context = factory.CreateCmsDbContext();
        (await context.Events.CountAsync(item => item.Status == CmsEventStatus.Processed)).Should().Be(2);
    }

    /// <summary>
    /// Verifies a delete event without payload or version is accepted.
    /// </summary>
    /// <remarks>Source business rule: spec scenario "Delete without payload or version".</remarks>
    [Fact]
    public async Task PostEvents_DeleteWithoutPayloadOrVersion_ReturnsCreated()
    {
        using var factory = new CmsWebhookApiFactory();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = Basic(CmsWebhookApiFactory.CmsUsername, CmsWebhookApiFactory.CmsPassword);

        var response = await client.PostAsync("/cms/events", Json("""{"type":"delete","id":"entity-1","timestamp":"2024-01-01T00:00:00Z"}"""));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        using var context = factory.CreateCmsDbContext();
        var @event = await context.Events.SingleAsync();
        @event.Type.Should().Be(CmsEventType.Delete);
        @event.Version.Should().BeNull();
        @event.Payload.Should().BeNull();
    }

    /// <summary>
    /// Verifies each validation failure returns 400 and records nothing.
    /// </summary>
    /// <remarks>Source business rule: spec "Validates and sanitizes events" — invalid requests are rejected
    /// with nothing recorded.</remarks>
    [Theory]
    [InlineData("""{"type":"deploy","id":"a","payload":{},"version":1,"timestamp":"2024-01-01T00:00:00Z"}""")]
    [InlineData("""{"id":"a","payload":{},"version":1,"timestamp":"2024-01-01T00:00:00Z"}""")]
    [InlineData("""{"type":"publish","id":"  ","payload":{},"version":1,"timestamp":"2024-01-01T00:00:00Z"}""")]
    [InlineData("""{"type":"publish","id":"a","payload":{},"version":1,"timestamp":"not-a-date"}""")]
    [InlineData("""{"type":"publish","id":"a","payload":{},"version":0,"timestamp":"2024-01-01T00:00:00Z"}""")]
    [InlineData("""{"type":"publish","id":"a","payload":[],"version":1,"timestamp":"2024-01-01T00:00:00Z"}""")]
    [InlineData("""{"type":"publish","id":"a","version":1,"timestamp":"2024-01-01T00:00:00Z"}""")]
    [InlineData("""{"type":"publish","id":"a","payload":5,"version":1,"timestamp":"2024-01-01T00:00:00Z"}""")]
    public async Task PostEvents_InvalidRequest_ReturnsBadRequestAndRecordsNothing(string body)
    {
        using var factory = new CmsWebhookApiFactory();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = Basic(CmsWebhookApiFactory.CmsUsername, CmsWebhookApiFactory.CmsPassword);

        var response = await client.PostAsync("/cms/events", Json(body));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        using var context = factory.CreateCmsDbContext();
        (await context.Events.CountAsync()).Should().Be(0);
    }

    /// <summary>
    /// Verifies malformed JSON and non-object/non-array roots return 400.
    /// </summary>
    /// <remarks>Source business rule: spec scenario "Malformed JSON body" and the single/batch contract.</remarks>
    [Theory]
    [InlineData("not json at all")]
    [InlineData("5")]
    [InlineData("[1, 2]")]
    public async Task PostEvents_MalformedBody_ReturnsBadRequest(string body)
    {
        using var factory = new CmsWebhookApiFactory();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = Basic(CmsWebhookApiFactory.CmsUsername, CmsWebhookApiFactory.CmsPassword);

        var response = await client.PostAsync("/cms/events", Json(body));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        using var context = factory.CreateCmsDbContext();
        (await context.Events.CountAsync()).Should().Be(0);
    }

    /// <summary>
    /// Verifies a batch containing an invalid event is rejected atomically with nothing recorded.
    /// </summary>
    /// <remarks>Source business rule: spec "Batch recording is atomic" — all-or-nothing.</remarks>
    [Fact]
    public async Task PostEvents_BatchWithInvalidEvent_ReturnsBadRequestAndRecordsNothing()
    {
        using var factory = new CmsWebhookApiFactory();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = Basic(CmsWebhookApiFactory.CmsUsername, CmsWebhookApiFactory.CmsPassword);
        var batch = """
            [
              {"type":"publish","id":"entity-1","payload":{"title":"hello"},"version":1,"timestamp":"2024-01-01T00:00:00Z"},
              {"type":"deploy","id":"entity-2","payload":{},"version":1,"timestamp":"2024-01-01T00:00:00Z"}
            ]
            """;

        var response = await client.PostAsync("/cms/events", Json(batch));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        using var context = factory.CreateCmsDbContext();
        (await context.Events.CountAsync()).Should().Be(0);
    }

    private static async Task<CmsEntity> WaitForEntityAsync(CmsWebhookApiFactory factory, string entityId)
    {
        using var context = factory.CreateCmsDbContext();
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (DateTime.UtcNow < deadline)
        {
            var entity = await context.Entities.SingleOrDefaultAsync(item => item.Id == entityId);
            if (entity is not null)
            {
                return entity;
            }

            await Task.Delay(100);
        }

        throw new Xunit.Sdk.XunitException($"Entity '{entityId}' was not processed within the timeout.");
    }

    private static string ValidPublish(string id, int version = 1)
        => $$"""
            {"type":"publish","id":"{{id}}","payload":{"title":"hello"},"version":{{version}},"timestamp":"2024-01-01T00:00:00Z"}
            """;

    private static StringContent Json(string body) => new(body, Encoding.UTF8, "application/json");

    private static AuthenticationHeaderValue Basic(string username, string password)
        => new("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes($"{username}:{password}")));
}
