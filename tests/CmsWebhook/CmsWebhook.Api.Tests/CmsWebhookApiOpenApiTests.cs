using System.Linq;
using System.Net;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;

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
    /// Verifies the Scalar API reference UI is served anonymously in non-Development environments.
    /// </summary>
    /// <remarks>
    /// Source business rule: spec "OpenAPI document", scenario "API reference UI served in all
    /// environments". The test host defaults to Development, so the host is explicitly switched to
    /// Production — without that, the test would pass even if the UI stayed Development-only (vacuous).
    /// The UI renders the same public contract JSON as <c>/openapi/v1.json</c>, hence the anonymous access.
    /// </remarks>
    [Fact]
    public async Task GetScalarUi_InProductionEnvironment_ReturnsOkAnonymously()
    {
        using var factory = new CmsWebhookApiFactory()
            .WithWebHostBuilder(builder => builder.UseEnvironment("Production"));
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/scalar/v1");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("text/html");
        (await response.Content.ReadAsStringAsync()).Should().NotBeEmpty();
    }

    /// <summary>
    /// Verifies the Scalar API reference UI remains reachable in the default Development environment.
    /// </summary>
    /// <remarks>
    /// Source business rule: the UI is always-on; the previous Development-only behavior is gone, so the
    /// default test-host environment must serve it too (regression against the environment guard).
    /// </remarks>
    [Fact]
    public async Task GetScalarUi_InDevelopmentEnvironment_ReturnsOk()
    {
        using var factory = new CmsWebhookApiFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/scalar/v1");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("text/html");
    }

    /// <summary>
    /// Verifies the served contract is accurate: the ingestion request body declares both accepted forms
    /// (single object or batch array) with the event fields, the responses match the runtime status codes
    /// (201/400/401/403/429, not a generated 200), and the Basic security scheme is declared.
    /// </summary>
    /// <remarks>
    /// Source business rule: spec "OpenAPI document" — the document stays in sync with the implemented
    /// endpoints and documents the ingestion request shape and authentication scheme (change
    /// improve-api-documentation).
    /// </remarks>
    [Fact]
    public async Task GetOpenApiDocument_DeclaresAccurateEventsContract()
    {
        using var factory = new CmsWebhookApiFactory();
        using var client = factory.CreateClient();

        var body = await client.GetStringAsync("/openapi/v1.json");
        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;
        var post = root.GetProperty("paths").GetProperty("/cms/events").GetProperty("post");

        var requestBody = post.GetProperty("requestBody");
        requestBody.GetProperty("required").GetBoolean().Should().BeTrue();
        var schema = requestBody.GetProperty("content").GetProperty("application/json").GetProperty("schema");
        var oneOf = schema.GetProperty("oneOf");
        oneOf.GetArrayLength().Should().Be(2);
        oneOf[0].GetProperty("type").GetString().Should().Be("object");
        oneOf[1].GetProperty("type").GetString().Should().Be("array");
        var properties = oneOf[0].GetProperty("properties");
        foreach (var field in new[] { "type", "id", "payload", "version", "timestamp" })
        {
            properties.TryGetProperty(field, out _).Should().BeTrue();
        }

        var typeEnum = properties.GetProperty("type").GetProperty("enum")
            .EnumerateArray().Select(item => item.GetString()).ToList();
        typeEnum.Should().BeEquivalentTo(new[] { "publish", "update", "unPublish", "delete" });

        var responses = post.GetProperty("responses");
        responses.TryGetProperty("201", out _).Should().BeTrue();
        responses.TryGetProperty("400", out var badRequest).Should().BeTrue();
        badRequest.GetProperty("description").GetString().Should().Contain("payload");
        responses.TryGetProperty("401", out _).Should().BeTrue();
        responses.TryGetProperty("403", out var forbidden).Should().BeTrue();
        forbidden.GetProperty("description").GetString()
            .Should().Be("The caller is authenticated but not authorized on this API.");
        responses.TryGetProperty("429", out var tooMany).Should().BeTrue();
        tooMany.GetProperty("description").GetString()
            .Should().Be("The caller exceeded the allowed request rate; retry later.");
        responses.TryGetProperty("200", out _).Should().BeFalse();

        var basic = root.GetProperty("components").GetProperty("securitySchemes").GetProperty("basic");
        basic.GetProperty("type").GetString().Should().Be("http");
        basic.GetProperty("scheme").GetString().Should().Be("basic");
        post.GetProperty("security")[0].TryGetProperty("basic", out _).Should().BeTrue();
        root.GetProperty("paths").GetProperty("/health").GetProperty("get")
            .TryGetProperty("security", out _).Should().BeFalse();
    }

    /// <summary>
    /// Verifies the served contract discloses no implementation details: no reserved username, shared
    /// credential store, or outbox phrasing in the descriptions.
    /// </summary>
    /// <remarks>
    /// Source business rule: spec "OpenAPI document", scenario "Contract does not disclose implementation
    /// details" (change sanitize-openapi-docs). The document is public (served anonymously), so a
    /// re-introduced internal phrase must fail this test.
    /// </remarks>
    [Fact]
    public async Task GetOpenApiDocument_DoesNotDiscloseImplementationDetails()
    {
        using var factory = new CmsWebhookApiFactory();
        using var client = factory.CreateClient();

        var body = await client.GetStringAsync("/openapi/v1.json");

        body.Should().NotContain("cms-webhook");
        body.Should().NotContain("shared credential store");
        body.Should().NotContain("outbox");
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
