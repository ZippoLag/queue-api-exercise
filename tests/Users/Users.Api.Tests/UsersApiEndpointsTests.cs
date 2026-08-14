using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using CmsWebhook.Domain;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Users.Api.Tests;

/// <summary>
/// Integration tests for the Users API entity endpoints: listing with role filtering, and the
/// administrator-only enable/disable commands.
/// </summary>
public class UsersApiEndpointsTests
{
    private const string DisabledEntityId = "disabled-1";
    private const string EnabledEntityId = "enabled-1";
    private const string UnpublishedEntityId = "unpublished-1";

    /// <summary>
    /// Verifies a request without an <c>Authorization</c> header is rejected with <c>401</c>.
    /// </summary>
    /// <remarks>
    /// Source business rule: spec "Users API authentication and roles", scenario
    /// "Request without credentials".
    /// </remarks>
    [Fact]
    public async Task GetEntities_WithoutCredentials_ReturnsUnauthorized()
    {
        using var factory = new UsersApiFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/entities");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await response.Content.ReadAsStringAsync()).Should().BeEmpty();
    }

    /// <summary>
    /// Verifies valid credentials of the cms user are rejected with <c>403</c> on the listing too.
    /// </summary>
    /// <remarks>
    /// Source business rule: spec "Users API authentication and roles", scenario
    /// "cms-webhook is rejected on the Users API" — the fallback policy applies to every protected endpoint.
    /// </remarks>
    [Fact]
    public async Task GetEntities_AsCmsWebhook_ReturnsForbidden()
    {
        using var factory = new UsersApiFactory();
        using var client = AuthenticatedClient(factory, UsersApiFactory.CmsUsername, UsersApiFactory.CmsPassword);

        var response = await client.GetAsync("/entities");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    /// <summary>
    /// Verifies a regular user sees only published entities that are not disabled by the administrator.
    /// </summary>
    /// <remarks>
    /// Source business rule: spec "Entities are listed by published status and administrator visibility",
    /// scenario "Regular user sees only published, enabled entities".
    /// </remarks>
    [Fact]
    public async Task GetEntities_AsRegularUser_ReturnsOnlyEnabledPublishedEntities()
    {
        using var factory = new UsersApiFactory();
        SeedStandardEntities(factory);
        using var client = AuthenticatedClient(factory, UsersApiFactory.RegularUsername, UsersApiFactory.RegularPassword);

        var response = await client.GetAsync("/entities");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var ids = await ReadEntityIdsAsync(response);
        ids.Should().Equal(EnabledEntityId);
    }

    /// <summary>
    /// Verifies the administrator sees all published entities, including disabled ones.
    /// </summary>
    /// <remarks>
    /// Source business rule: spec "Entities are listed by published status and administrator visibility",
    /// scenario "Administrator sees all published entities".
    /// </remarks>
    [Fact]
    public async Task GetEntities_AsAdministrator_ReturnsAllPublishedIncludingDisabled()
    {
        using var factory = new UsersApiFactory();
        SeedStandardEntities(factory);
        using var client = AuthenticatedClient(factory, UsersApiFactory.AdministratorUsername, UsersApiFactory.AdministratorPassword);

        var response = await client.GetAsync("/entities");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var ids = await ReadEntityIdsAsync(response);
        ids.Should().BeEquivalentTo(new[] { EnabledEntityId, DisabledEntityId });
    }

    /// <summary>
    /// Verifies unpublished entities are never listed, for any role.
    /// </summary>
    /// <remarks>
    /// Source business rule: spec "Entities are listed by published status and administrator visibility",
    /// scenario "Unpublished entities are never listed" — even the administrator must not see them.
    /// </remarks>
    [Fact]
    public async Task GetEntities_AsAdministrator_ExcludesUnpublished()
    {
        using var factory = new UsersApiFactory();
        SeedStandardEntities(factory);
        using var client = AuthenticatedClient(factory, UsersApiFactory.AdministratorUsername, UsersApiFactory.AdministratorPassword);

        var response = await client.GetAsync("/entities");

        var ids = await ReadEntityIdsAsync(response);
        ids.Should().NotContain(UnpublishedEntityId);
    }

    /// <summary>
    /// Verifies each returned item includes the id, administrator-visibility flag, version, update time
    /// and payload.
    /// </summary>
    /// <remarks>
    /// Source business rule: spec scenario "Response items include the entity id and visibility flag" —
    /// the administrator's listing must expose disabled entities with their flag so they can be targeted.
    /// </remarks>
    [Fact]
    public async Task GetEntities_ItemsCarryIdVisibilityVersionUpdatedAtAndPayload()
    {
        using var factory = new UsersApiFactory();
        SeedStandardEntities(factory);
        using var client = AuthenticatedClient(factory, UsersApiFactory.AdministratorUsername, UsersApiFactory.AdministratorPassword);

        var body = await client.GetStringAsync("/entities");

        using var document = JsonDocument.Parse(body);
        var items = document.RootElement.EnumerateArray().ToList();
        var disabled = items.Single(item => item.GetProperty("id").GetString() == DisabledEntityId);

        disabled.TryGetProperty("isVisibleByAdmin", out var visibility).Should().BeTrue();
        visibility.GetBoolean().Should().BeFalse();
        disabled.TryGetProperty("latestVersion", out var version).Should().BeTrue();
        version.GetInt32().Should().Be(2);
        disabled.TryGetProperty("updatedAt", out var updatedAt).Should().BeTrue();
        updatedAt.GetString().Should().NotBeNullOrEmpty();
        disabled.TryGetProperty("payload", out var payload).Should().BeTrue();
        payload.GetString().Should().Be("""{"title":"disabled"}""");
    }

    /// <summary>
    /// Verifies the administrator's disable flips the flag and hides the entity from regular users.
    /// </summary>
    /// <remarks>
    /// Source business rule: spec "Administrator enables and disables entity visibility", scenario
    /// "Administrator disables an entity".
    /// </remarks>
    [Fact]
    public async Task PostDisable_AsAdministrator_ReturnsNoContentAndHidesFromRegularUsers()
    {
        using var factory = new UsersApiFactory();
        SeedStandardEntities(factory);
        using var adminClient = AuthenticatedClient(factory, UsersApiFactory.AdministratorUsername, UsersApiFactory.AdministratorPassword);

        var response = await adminClient.PostAsync($"/entities/{EnabledEntityId}/disable", null);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await response.Content.ReadAsStringAsync()).Should().BeEmpty();

        using var regularClient = AuthenticatedClient(factory, UsersApiFactory.RegularUsername, UsersApiFactory.RegularPassword);
        var listing = await regularClient.GetAsync("/entities");
        var ids = await ReadEntityIdsAsync(listing);
        ids.Should().NotContain(EnabledEntityId);
    }

    /// <summary>
    /// Verifies the administrator's enable restores the entity to regular users' listings.
    /// </summary>
    /// <remarks>
    /// Source business rule: spec "Administrator enables and disables entity visibility", scenario
    /// "Administrator enables a disabled entity".
    /// </remarks>
    [Fact]
    public async Task PostEnable_AsAdministrator_ReturnsNoContentAndRestoresVisibility()
    {
        using var factory = new UsersApiFactory();
        SeedStandardEntities(factory);
        using var adminClient = AuthenticatedClient(factory, UsersApiFactory.AdministratorUsername, UsersApiFactory.AdministratorPassword);

        var response = await adminClient.PostAsync($"/entities/{DisabledEntityId}/enable", null);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await response.Content.ReadAsStringAsync()).Should().BeEmpty();

        using var regularClient = AuthenticatedClient(factory, UsersApiFactory.RegularUsername, UsersApiFactory.RegularPassword);
        var listing = await regularClient.GetAsync("/entities");
        var ids = await ReadEntityIdsAsync(listing);
        ids.Should().Contain(DisabledEntityId);
    }

    /// <summary>
    /// Verifies disabling an already-disabled entity still succeeds (idempotent).
    /// </summary>
    /// <remarks>
    /// Source business rule: spec scenario "Disabling is idempotent".
    /// </remarks>
    [Fact]
    public async Task PostDisable_AlreadyDisabled_ReturnsNoContent()
    {
        using var factory = new UsersApiFactory();
        SeedStandardEntities(factory);
        using var client = AuthenticatedClient(factory, UsersApiFactory.AdministratorUsername, UsersApiFactory.AdministratorPassword);

        var response = await client.PostAsync($"/entities/{DisabledEntityId}/disable", null);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    /// <summary>
    /// Verifies enabling an already-enabled entity still succeeds (idempotent).
    /// </summary>
    /// <remarks>
    /// Source business rule: both commands are idempotent (design decision 4 of the users-api-vertical
    /// change); writing the current value back is an empty success.
    /// </remarks>
    [Fact]
    public async Task PostEnable_AlreadyEnabled_ReturnsNoContent()
    {
        using var factory = new UsersApiFactory();
        SeedStandardEntities(factory);
        using var client = AuthenticatedClient(factory, UsersApiFactory.AdministratorUsername, UsersApiFactory.AdministratorPassword);

        var response = await client.PostAsync($"/entities/{EnabledEntityId}/enable", null);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    /// <summary>
    /// Verifies an unknown entity id yields <c>404</c> for both commands.
    /// </summary>
    /// <remarks>
    /// Source business rule: spec scenario "Unknown entity id".
    /// </remarks>
    [Theory]
    [InlineData("disable")]
    [InlineData("enable")]
    public async Task PostCommand_UnknownId_ReturnsNotFound(string command)
    {
        using var factory = new UsersApiFactory();
        SeedStandardEntities(factory);
        using var client = AuthenticatedClient(factory, UsersApiFactory.AdministratorUsername, UsersApiFactory.AdministratorPassword);

        var response = await client.PostAsync($"/entities/no-such-entity/{command}", null);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    /// <summary>
    /// Verifies a regular user cannot disable an entity: <c>403</c> and the handler is not executed.
    /// </summary>
    /// <remarks>
    /// Source business rule: spec "Users API authentication and roles", scenario
    /// "Regular user cannot disable an entity" — the handler must not run, so the flag stays unchanged.
    /// </remarks>
    [Fact]
    public async Task PostDisable_AsRegularUser_ReturnsForbiddenAndDoesNotExecute()
    {
        using var factory = new UsersApiFactory();
        SeedStandardEntities(factory);
        using var client = AuthenticatedClient(factory, UsersApiFactory.RegularUsername, UsersApiFactory.RegularPassword);

        var response = await client.PostAsync($"/entities/{EnabledEntityId}/disable", null);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        using var context = factory.CreateUsersDbContext();
        var entity = await context.Entities.FindAsync(EnabledEntityId);
        entity!.IsVisibleByAdmin.Should().BeTrue();
    }

    /// <summary>
    /// Verifies the cms user is rejected on the enable/disable endpoints with <c>403</c>.
    /// </summary>
    /// <remarks>
    /// Source business rule: spec "Users API authentication and roles", scenario
    /// "cms-webhook is rejected on the Users API".
    /// </remarks>
    [Fact]
    public async Task PostDisable_AsCmsWebhook_ReturnsForbidden()
    {
        using var factory = new UsersApiFactory();
        SeedStandardEntities(factory);
        using var client = AuthenticatedClient(factory, UsersApiFactory.CmsUsername, UsersApiFactory.CmsPassword);

        var response = await client.PostAsync($"/entities/{EnabledEntityId}/disable", null);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    /// <summary>
    /// Verifies an authenticated unknown user is treated as a regular user for the listing.
    /// </summary>
    /// <remarks>
    /// Source business rule: spec "Users API authentication and roles" — every other valid username is a
    /// regular user (design decision 1); the seeded store only contains the three reserved users, so the
    /// extra user is injected through the credential-provider seam like the CMS tests do.
    /// </remarks>
    [Fact]
    public async Task GetEntities_AsOtherValidUser_IsTreatedAsRegularUser()
    {
        using var baseFactory = new UsersApiFactory();
        SeedStandardEntities(baseFactory);
        using var factory = baseFactory.WithCredentialProvider(
            (UsersApiFactory.AdministratorUsername, UsersApiFactory.AdministratorPassword),
            (UsersApiFactory.RegularUsername, UsersApiFactory.RegularPassword),
            (UsersApiFactory.CmsUsername, UsersApiFactory.CmsPassword),
            ("other-client", "dddddddd-dddd-dddd-dddd-dddddddddddd"));
        using var client = AuthenticatedClient(factory, "other-client", "dddddddd-dddd-dddd-dddd-dddddddddddd");

        var response = await client.GetAsync("/entities");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var ids = await ReadEntityIdsAsync(response);
        ids.Should().Equal(EnabledEntityId);
    }

    private static void SeedStandardEntities(UsersApiFactory factory)
        => factory.SeedEntities(
            Entity(EnabledEntityId, isPublished: true, isVisibleByAdmin: true, version: 1, """{"title":"enabled"}"""),
            Entity(DisabledEntityId, isPublished: true, isVisibleByAdmin: false, version: 2, """{"title":"disabled"}"""),
            Entity(UnpublishedEntityId, isPublished: false, isVisibleByAdmin: true, version: 3, """{"title":"unpublished"}"""));

    private static CmsEntity Entity(string id, bool isPublished, bool isVisibleByAdmin, int version, string payload)
        => new()
        {
            Id = id,
            IsPublished = isPublished,
            IsVisibleByAdmin = isVisibleByAdmin,
            LatestVersion = version,
            Payload = payload,
            UpdatedAt = new DateTimeOffset(2024, 6, 7, 8, 9, 10, TimeSpan.Zero),
        };

    private static async Task<List<string>> ReadEntityIdsAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(body);
        return document.RootElement
            .EnumerateArray()
            .Select(item => item.GetProperty("id").GetString()!)
            .ToList();
    }

    private static HttpClient AuthenticatedClient(WebApplicationFactory<Program> factory, string username, string password)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = Basic(username, password);
        return client;
    }

    private static AuthenticationHeaderValue Basic(string username, string password)
        => new("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes($"{username}:{password}")));
}
