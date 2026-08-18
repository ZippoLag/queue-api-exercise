using System.Net;
using System.Text;
using System.Text.Json;
using FluentAssertions;

namespace QueueApi.E2E.Tests;

/// <summary>
/// End-to-end smoke tests: both APIs running against one seeded credential store and one CMS database,
/// exercising the full vertical — CMS event ingestion, asynchronous outbox processing into the shared
/// entity store, the Users API listing, and the administrator's visibility control.
/// </summary>
public class EndToEndSmokeTests
{
    /// <summary>
    /// Verifies a CMS event accepted by the CMS Webhook API becomes visible on the Users API listing,
    /// and that the cms client is rejected there.
    /// </summary>
    /// <remarks>
    /// Source business rule: the vertical's contract — the CMS Webhook API writes the shared
    /// <c>cms_entities</c> store (spec "Entities are listed by published status and administrator
    /// visibility") and the Users API reads it; <c>cms-webhook</c> is rejected on the Users API
    /// (spec "Users API authentication and roles").
    /// </remarks>
    [Fact]
    public async Task CmsEventPublishedOnCmsApi_BecomesVisibleOnUsersApi()
    {
        using var environment = new E2EEnvironment();

        // Anonymous liveness probes on both APIs against the same stores.
        (await environment.CmsClient.GetAsync("/health")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await environment.UsersClient.GetAsync("/health")).StatusCode.Should().Be(HttpStatusCode.OK);

        // The CMS pushes a publish event through the CMS Webhook API.
        using var cmsClient = environment.CreateCmsApiClient(E2EEnvironment.CmsUsername, E2EEnvironment.CmsPassword);
        var ingest = await cmsClient.PostAsync("/cms/events", Json(Publish("entity-1")));
        ingest.StatusCode.Should().Be(HttpStatusCode.Created);

        // The outbox worker eventually materializes the entity; the Users API lists it for a regular user.
        using var regularClient = environment.CreateUsersApiClient(E2EEnvironment.RegularUsername, E2EEnvironment.RegularPassword);
        var ids = await WaitForEntitiesAsync(regularClient, "entity-1");
        ids.Should().Contain("entity-1");

        // Valid cms credentials are rejected on the Users API (reserved for the CMS API).
        using var cmsOnUsers = environment.CreateUsersApiClient(E2EEnvironment.CmsUsername, E2EEnvironment.CmsPassword);
        (await cmsOnUsers.GetAsync("/entities")).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // The listed item carries the payload the CMS sent and is visible by default.
        var body = await regularClient.GetStringAsync("/entities");
        using var document = JsonDocument.Parse(body);
        var item = document.RootElement.EnumerateArray()
            .Single(element => element.GetProperty("id").GetString() == "entity-1");
        item.GetProperty("payload").GetString().Should().Be("""{"title":"hello"}""");
        item.GetProperty("isVisibleByAdmin").GetBoolean().Should().BeTrue();
    }

    /// <summary>
    /// Verifies the administrator's enable/disable control flows through the shared store: a disabled
    /// entity disappears from regular users' listings (but stays visible to the administrator with its
    /// flag), and enabling restores it.
    /// </summary>
    /// <remarks>
    /// Source business rule: spec "Administrator enables and disables entity visibility" — disabled
    /// entities are hidden from regular users' listings; enabling restores them; both commands are
    /// idempotent <c>204</c> responses. Exercised end to end through the CMS ingestion path so the
    /// entities arrive via the outbox, not a test seed.
    /// </remarks>
    [Fact]
    public async Task AdministratorDisableAndEnable_TogglesVisibilityForRegularUsers()
    {
        using var environment = new E2EEnvironment();

        using var cmsClient = environment.CreateCmsApiClient(E2EEnvironment.CmsUsername, E2EEnvironment.CmsPassword);
        (await cmsClient.PostAsync("/cms/events", Json(Publish("entity-1")))).StatusCode.Should().Be(HttpStatusCode.Created);
        (await cmsClient.PostAsync("/cms/events", Json(Publish("entity-2")))).StatusCode.Should().Be(HttpStatusCode.Created);

        using var regularClient = environment.CreateUsersApiClient(E2EEnvironment.RegularUsername, E2EEnvironment.RegularPassword);
        await WaitForEntitiesAsync(regularClient, "entity-1", "entity-2");

        using var adminClient = environment.CreateUsersApiClient(E2EEnvironment.AdministratorUsername, E2EEnvironment.AdministratorPassword);

        // Disabling hides the entity from regular users; the other entity stays visible.
        var disable = await adminClient.PostAsync("/entities/entity-1/disable", null);
        disable.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var regularIds = await ReadEntityIdsAsync(await regularClient.GetAsync("/entities"));
        regularIds.Should().NotContain("entity-1");
        regularIds.Should().Contain("entity-2");

        // The administrator still sees the disabled entity, flagged as such.
        var adminBody = await adminClient.GetStringAsync("/entities");
        using var adminDocument = JsonDocument.Parse(adminBody);
        var disabledItem = adminDocument.RootElement.EnumerateArray()
            .Single(element => element.GetProperty("id").GetString() == "entity-1");
        disabledItem.GetProperty("isVisibleByAdmin").GetBoolean().Should().BeFalse();

        // Disabling again is still an empty success (idempotent).
        (await adminClient.PostAsync("/entities/entity-1/disable", null)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Enabling restores the entity to regular users.
        (await adminClient.PostAsync("/entities/entity-1/enable", null)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        var restoredIds = await ReadEntityIdsAsync(await regularClient.GetAsync("/entities"));
        restoredIds.Should().BeEquivalentTo(new[] { "entity-1", "entity-2" });
    }

    /// <summary>
    /// Verifies the administrator's disable survives a subsequent CMS update event: the outbox worker
    /// rewrites the stored entity (version 2) without silently re-enabling it.
    /// </summary>
    /// <remarks>
    /// Source business rule: users-api design decision 3 — the entity upsert carries the stored
    /// administrator-visibility override forward, so a processed CMS event can never reset a disabled
    /// entity to visible. Proven over the full pipeline: ingest -> disable -> update event -> the entity
    /// stays hidden from regular users while its new version is visible to the administrator.
    /// </remarks>
    [Fact]
    public async Task SubsequentCmsUpdateEvent_DoesNotResetAdministratorDisable()
    {
        using var environment = new E2EEnvironment();

        using var cmsClient = environment.CreateCmsApiClient(E2EEnvironment.CmsUsername, E2EEnvironment.CmsPassword);
        (await cmsClient.PostAsync("/cms/events", Json(Publish("entity-1")))).StatusCode.Should().Be(HttpStatusCode.Created);

        using var regularClient = environment.CreateUsersApiClient(E2EEnvironment.RegularUsername, E2EEnvironment.RegularPassword);
        await WaitForEntitiesAsync(regularClient, "entity-1");

        using var adminClient = environment.CreateUsersApiClient(E2EEnvironment.AdministratorUsername, E2EEnvironment.AdministratorPassword);
        (await adminClient.PostAsync("/entities/entity-1/disable", null)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        // A newer CMS update rewrites the stored entity; the administrator's disable must survive.
        var update = await cmsClient.PostAsync("/cms/events", Json(Update("entity-1", version: 2)));
        update.StatusCode.Should().Be(HttpStatusCode.Created);

        // Wait until the update was processed into the shared store (the admin listing shows version 2).
        await WaitForAdminVersionAsync(adminClient, "entity-1", expectedVersion: 2);

        // The entity is still hidden from regular users and still flagged disabled for the administrator.
        var regularIds = await ReadEntityIdsAsync(await regularClient.GetAsync("/entities"));
        regularIds.Should().NotContain("entity-1");

        var adminBody = await adminClient.GetStringAsync("/entities");
        using var adminDocument = JsonDocument.Parse(adminBody);
        var item = adminDocument.RootElement.EnumerateArray()
            .Single(element => element.GetProperty("id").GetString() == "entity-1");
        item.GetProperty("isVisibleByAdmin").GetBoolean().Should().BeFalse();
        item.GetProperty("latestVersion").GetInt32().Should().Be(2);
    }

    /// <summary>
    /// Verifies an ingested event whose timestamp is not an ISO 8601 / RFC 3339 date-time is rejected
    /// with <c>400</c> and its unique entity id never materializes on the Users API.
    /// </summary>
    /// <remarks>
    /// Source business rule: spec "Validates and sanitizes events", scenario "Invalid timestamp" — the
    /// date-only form is rejected; the unique id proves nothing was recorded (a rejected request never
    /// enters the outbox, so absence is immediate and deterministic).
    /// </remarks>
    [Fact]
    public async Task IngestWithNonRfc3339Timestamp_IsRejectedAndNeverListed()
    {
        using var environment = new E2EEnvironment();
        using var cmsClient = environment.CreateCmsApiClient(E2EEnvironment.CmsUsername, E2EEnvironment.CmsPassword);

        var ingest = await cmsClient.PostAsync("/cms/events", Json(
            """{"type":"publish","id":"reject-ts-1","payload":{},"version":1,"timestamp":"2024-01-01"}"""));

        ingest.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        using var regularClient = environment.CreateUsersApiClient(E2EEnvironment.RegularUsername, E2EEnvironment.RegularPassword);
        var ids = await ReadEntityIdsAsync(await regularClient.GetAsync("/entities"));
        ids.Should().NotContain("reject-ts-1");
    }

    /// <summary>
    /// Verifies an ingested event whose payload is not a JSON object is rejected with <c>400</c> and its
    /// unique entity id never materializes on the Users API.
    /// </summary>
    /// <remarks>
    /// Source business rule: spec "Validates and sanitizes events", scenario "Payload is not a JSON
    /// object" — only key/value objects are accepted; the unique id proves nothing was recorded.
    /// </remarks>
    [Fact]
    public async Task IngestWithNonObjectPayload_IsRejectedAndNeverListed()
    {
        using var environment = new E2EEnvironment();
        using var cmsClient = environment.CreateCmsApiClient(E2EEnvironment.CmsUsername, E2EEnvironment.CmsPassword);

        var ingest = await cmsClient.PostAsync("/cms/events", Json(
            """{"type":"publish","id":"reject-payload-1","payload":[],"version":1,"timestamp":"2024-01-01T00:00:00Z"}"""));

        ingest.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        using var regularClient = environment.CreateUsersApiClient(E2EEnvironment.RegularUsername, E2EEnvironment.RegularPassword);
        var ids = await ReadEntityIdsAsync(await regularClient.GetAsync("/entities"));
        ids.Should().NotContain("reject-payload-1");
    }

    /// <summary>
    /// Verifies anonymous requests to protected endpoints of both APIs are rejected with <c>401</c>.
    /// </summary>
    /// <remarks>
    /// Source business rule: spec "All endpoints require authentication" (CMS Webhook API) and "Users API
    /// authentication and roles", scenario "Request without credentials" — the smoke vertical asserts the
    /// true no-credentials case, without any <c>Authorization</c> header.
    /// </remarks>
    [Fact]
    public async Task AnonymousRequestsToProtectedEndpoints_ReturnUnauthorized()
    {
        using var environment = new E2EEnvironment();

        var cmsPost = await environment.CmsClient.PostAsync("/cms/events", Json(Publish("entity-x")));
        cmsPost.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var usersGet = await environment.UsersClient.GetAsync("/entities");
        usersGet.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>
    /// Verifies an empty or whitespace-only route id is rejected with <c>400</c> on both enable/disable
    /// commands.
    /// </summary>
    /// <remarks>
    /// Source business rule: spec "Administrator enables and disables entity visibility", scenario
    /// "Empty or whitespace-only id" — the id is sanitized like the webhook's; an only-whitespace id is a
    /// client error, not an unknown entity.
    /// </remarks>
    [Theory]
    [InlineData("disable")]
    [InlineData("enable")]
    public async Task EmptyOrWhitespaceId_ReturnsBadRequest(string command)
    {
        using var environment = new E2EEnvironment();
        using var adminClient = environment.CreateUsersApiClient(E2EEnvironment.AdministratorUsername, E2EEnvironment.AdministratorPassword);

        var response = await adminClient.PostAsync($"/entities/%20%20/{command}", null);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>
    /// Verifies a route id carrying surrounding whitespace resolves to the stored entity: the trimmed
    /// disable applies and the entity disappears from regular users' listings.
    /// </summary>
    /// <remarks>
    /// Source business rule: spec "Administrator enables and disables entity visibility", scenario "Id is
    /// trimmed before lookup" — the padded-id entity is ingested through the CMS path, so the trim is
    /// proven end to end over the shared store.
    /// </remarks>
    [Fact]
    public async Task PaddedId_IsTrimmedBeforeLookup()
    {
        using var environment = new E2EEnvironment();
        using var cmsClient = environment.CreateCmsApiClient(E2EEnvironment.CmsUsername, E2EEnvironment.CmsPassword);
        (await cmsClient.PostAsync("/cms/events", Json(Publish("padded-1")))).StatusCode.Should().Be(HttpStatusCode.Created);

        using var regularClient = environment.CreateUsersApiClient(E2EEnvironment.RegularUsername, E2EEnvironment.RegularPassword);
        await WaitForEntitiesAsync(regularClient, "padded-1");

        using var adminClient = environment.CreateUsersApiClient(E2EEnvironment.AdministratorUsername, E2EEnvironment.AdministratorPassword);
        var disable = await adminClient.PostAsync("/entities/%20padded-1%20/disable", null);
        disable.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var ids = await ReadEntityIdsAsync(await regularClient.GetAsync("/entities"));
        ids.Should().NotContain("padded-1");
    }

    /// <summary>
    /// Verifies an unknown entity id yields <c>404</c> on both enable/disable commands.
    /// </summary>
    /// <remarks>
    /// Source business rule: spec "Administrator enables and disables entity visibility", scenario
    /// "Unknown entity id".
    /// </remarks>
    [Theory]
    [InlineData("disable")]
    [InlineData("enable")]
    public async Task UnknownId_ReturnsNotFound(string command)
    {
        using var environment = new E2EEnvironment();
        using var adminClient = environment.CreateUsersApiClient(E2EEnvironment.AdministratorUsername, E2EEnvironment.AdministratorPassword);

        var response = await adminClient.PostAsync($"/entities/no-such-entity/{command}", null);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    /// <summary>
    /// Polls the Users API listing until every expected entity id is present, proving the outbox worker
    /// processed the ingested events into the shared store.
    /// </summary>
    /// <param name="client">An authenticated Users API client.</param>
    /// <param name="expectedIds">The entity ids the listing must contain.</param>
    /// <returns>The ids of the last observed listing.</returns>
    /// <exception cref="Xunit.Sdk.XunitException">The expected entities were not listed within the timeout.</exception>
    private static async Task<List<string>> WaitForEntitiesAsync(HttpClient client, params string[] expectedIds)
    {
        var expected = expectedIds.ToHashSet(StringComparer.Ordinal);
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (DateTime.UtcNow < deadline)
        {
            using var response = await client.GetAsync("/entities");
            var ids = await ReadEntityIdsAsync(response);
            if (expected.IsSubsetOf(ids))
            {
                return ids;
            }

            await Task.Delay(100);
        }

        throw new Xunit.Sdk.XunitException(
            $"Entities [{string.Join(", ", expectedIds)}] were not listed within the timeout.");
    }

    /// <summary>
    /// Polls the administrator listing until the entity reports the expected latest version, proving a
    /// CMS event was processed into the shared store.
    /// </summary>
    /// <param name="adminClient">An administrator-authenticated Users API client.</param>
    /// <param name="entityId">The entity whose version to observe.</param>
    /// <param name="expectedVersion">The version the processed event must have written.</param>
    /// <exception cref="Xunit.Sdk.XunitException">The entity did not reach the version within the timeout.</exception>
    private static async Task WaitForAdminVersionAsync(HttpClient adminClient, string entityId, int expectedVersion)
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (DateTime.UtcNow < deadline)
        {
            using var response = await adminClient.GetAsync("/entities");
            var body = await response.Content.ReadAsStringAsync();
            using var document = JsonDocument.Parse(body);
            var item = document.RootElement.EnumerateArray()
                .FirstOrDefault(element => element.GetProperty("id").GetString() == entityId);
            if (item.ValueKind == JsonValueKind.Object
                && item.GetProperty("latestVersion").GetInt32() == expectedVersion)
            {
                return;
            }

            await Task.Delay(100);
        }

        throw new Xunit.Sdk.XunitException(
            $"Entity '{entityId}' did not reach version {expectedVersion} in the admin listing within the timeout.");
    }

    private static async Task<List<string>> ReadEntityIdsAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(body);
        return document.RootElement
            .EnumerateArray()
            .Select(item => item.GetProperty("id").GetString()!)
            .ToList();
    }

    private static StringContent Json(string body) => new(body, Encoding.UTF8, "application/json");

    private static string Publish(string id)
        => $$"""{"type":"publish","id":"{{id}}","payload":{"title":"hello"},"version":1,"timestamp":"2024-01-01T00:00:00Z"}""";

    private static string Update(string id, int version)
        => $$"""{"type":"update","id":"{{id}}","payload":{"title":"updated"},"version":{{version}},"timestamp":"2024-01-01T00:00:01Z"}""";
}
