using System.Net;
using System.Text;
using System.Text.Json;
using FluentAssertions;

namespace CmsWebhook.Api.Tests;

/// <summary>
/// Integration tests for the generated OpenAPI document at <c>/openapi/v1.json</c>.
/// </summary>
public class CmsWebhookApiOpenApiTests
{
    /// <summary>
    /// Verifies the OpenAPI document is reachable anonymously and served as JSON.
    /// </summary>
    /// <remarks>
    /// Source business rule: spec "OpenAPI document", scenario "Contract served anonymously".
    /// </remarks>
    [Fact]
    public async Task GetOpenApiDocument_WithoutAuthorization_ReturnsOk()
    {
        using var factory = new CmsWebhookApiFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/openapi/v1.json");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/json");
        (await response.Content.ReadAsStringAsync()).Should().NotBeEmpty();
    }

    /// <summary>
    /// Verifies the served document describes the API's endpoints.
    /// </summary>
    /// <remarks>
    /// Source business rule: spec "OpenAPI document", scenario "Contract describes the endpoints" — the
    /// document must list <c>/cms/events</c> (POST) and <c>/health</c> (GET).
    /// </remarks>
    [Fact]
    public async Task GetOpenApiDocument_DescribesCmsEventsAndHealthEndpoints()
    {
        using var factory = new CmsWebhookApiFactory();
        using var client = factory.CreateClient();

        var body = await client.GetStringAsync("/openapi/v1.json");

        using var document = JsonDocument.Parse(body);
        var paths = document.RootElement.GetProperty("paths");

        paths.TryGetProperty("/cms/events", out var cmsEvents).Should().BeTrue();
        cmsEvents.TryGetProperty("post", out _).Should().BeTrue();

        paths.TryGetProperty("/health", out var health).Should().BeTrue();
        health.TryGetProperty("get", out _).Should().BeTrue();
    }

    /// <summary>
    /// Verifies the anonymous carve-out is scoped to the health and contract endpoints.
    /// </summary>
    /// <remarks>
    /// Source business rule: spec "All endpoints require authentication" — <c>/cms/events</c> must still
    /// reject unauthenticated requests even though <c>/health</c> and <c>/openapi/v1.json</c> are anonymous.
    /// </remarks>
    [Fact]
    public async Task PostEvents_WithoutAuthorization_ReturnsUnauthorized()
    {
        using var factory = new CmsWebhookApiFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsync("/cms/events", Json(ValidPublish()));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private static StringContent Json(string body) => new(body, Encoding.UTF8, "application/json");

    private static string ValidPublish(string id = "entity-1")
        => $$"""{"type":"publish","id":"{{id}}","payload":{"title":"hello"},"version":1,"timestamp":"2024-01-01T00:00:00Z"}""";
}
