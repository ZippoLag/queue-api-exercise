using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;

namespace Users.Api.Tests;

/// <summary>
/// Integration tests for the generated OpenAPI document at <c>/openapi/v1.json</c>, the anonymous
/// healthcheck and the always-on Scalar UI.
/// </summary>
public class UsersApiOpenApiTests
{
    /// <summary>
    /// Verifies the OpenAPI document is reachable anonymously and served as JSON.
    /// </summary>
    /// <remarks>
    /// Source business rule: spec "Users API authentication and roles", scenario
    /// "Anonymous discovery endpoints".
    /// </remarks>
    [Fact]
    public async Task GetOpenApiDocument_WithoutAuthorization_ReturnsOk()
    {
        using var factory = new UsersApiFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/openapi/v1.json");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/json");
        (await response.Content.ReadAsStringAsync()).Should().NotBeEmpty();
    }

    /// <summary>
    /// Verifies the served document describes the entity listing and enable/disable endpoints.
    /// </summary>
    /// <remarks>
    /// Source business rule: task 6.2 — the OpenAPI contract describes <c>/entities</c>,
    /// <c>/entities/{id}/disable</c> and <c>/entities/{id}/enable</c>.
    /// </remarks>
    [Fact]
    public async Task GetOpenApiDocument_DescribesEntitiesAndHealthEndpoints()
    {
        using var factory = new UsersApiFactory();
        using var client = factory.CreateClient();

        var body = await client.GetStringAsync("/openapi/v1.json");

        using var document = JsonDocument.Parse(body);
        var paths = document.RootElement.GetProperty("paths");

        paths.TryGetProperty("/entities", out var entities).Should().BeTrue();
        entities.TryGetProperty("get", out _).Should().BeTrue();

        paths.TryGetProperty("/entities/{id}/disable", out var disable).Should().BeTrue();
        disable.TryGetProperty("post", out _).Should().BeTrue();

        paths.TryGetProperty("/entities/{id}/enable", out var enable).Should().BeTrue();
        enable.TryGetProperty("post", out _).Should().BeTrue();

        paths.TryGetProperty("/health", out var health).Should().BeTrue();
        health.TryGetProperty("get", out _).Should().BeTrue();
    }

    /// <summary>
    /// Verifies the served contract is accurate: the listing declares its item schema and responses, the
    /// enable/disable commands declare 204/401/403/404 (not a generated 200), and the Basic security
    /// scheme is declared.
    /// </summary>
    /// <remarks>
    /// Source business rule: spec "OpenAPI document" — the document stays in sync with the implemented
    /// endpoints and declares the authentication scheme (change improve-api-documentation).
    /// </remarks>
    [Fact]
    public async Task GetOpenApiDocument_DeclaresAccurateEntitiesContract()
    {
        using var factory = new UsersApiFactory();
        using var client = factory.CreateClient();

        var body = await client.GetStringAsync("/openapi/v1.json");
        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;
        var paths = root.GetProperty("paths");

        var entities = paths.GetProperty("/entities").GetProperty("get");
        var entitiesResponses = entities.GetProperty("responses");
        entitiesResponses.TryGetProperty("200", out var ok).Should().BeTrue();
        var items = ok.GetProperty("content").GetProperty("application/json").GetProperty("schema");
        items.GetProperty("type").GetString().Should().Be("array");
        var itemProperties = items.GetProperty("items").GetProperty("properties");
        foreach (var field in new[] { "Id", "IsVisibleByAdmin", "LatestVersion", "UpdatedAt", "Payload" })
        {
            itemProperties.TryGetProperty(field, out _).Should().BeTrue();
        }

        entitiesResponses.TryGetProperty("401", out _).Should().BeTrue();
        entitiesResponses.TryGetProperty("403", out _).Should().BeTrue();

        foreach (var path in new[] { "/entities/{id}/disable", "/entities/{id}/enable" })
        {
            var responses = paths.GetProperty(path).GetProperty("post").GetProperty("responses");
            responses.TryGetProperty("204", out _).Should().BeTrue();
            responses.TryGetProperty("400", out var badRequest).Should().BeTrue();
            badRequest.GetProperty("description").GetString()
                .Should().Be("The id is empty or whitespace-only.");
            responses.TryGetProperty("401", out _).Should().BeTrue();
            responses.TryGetProperty("403", out _).Should().BeTrue();
            responses.TryGetProperty("404", out _).Should().BeTrue();
            responses.TryGetProperty("200", out _).Should().BeFalse();
        }

        var basic = root.GetProperty("components").GetProperty("securitySchemes").GetProperty("basic");
        basic.GetProperty("type").GetString().Should().Be("http");
        basic.GetProperty("scheme").GetString().Should().Be("basic");
        paths.GetProperty("/entities").GetProperty("get").GetProperty("security")[0]
            .TryGetProperty("basic", out _).Should().BeTrue();
    }

    /// <summary>
    /// Verifies the served contract discloses no implementation details: no reserved username, shared
    /// credential store, or outbox phrasing, and the listing 403 uses generic authorization wording.
    /// </summary>
    /// <remarks>
    /// Source business rule: spec "OpenAPI document", scenario "Contract does not disclose implementation
    /// details" (change sanitize-openapi-docs). Asserting on the served document (not the source strings)
    /// makes the test a contract guard against re-introduced leaks.
    /// </remarks>
    [Fact]
    public async Task GetOpenApiDocument_DoesNotDiscloseImplementationDetails()
    {
        using var factory = new UsersApiFactory();
        using var client = factory.CreateClient();

        var body = await client.GetStringAsync("/openapi/v1.json");

        body.Should().NotContain("cms-webhook");
        body.Should().NotContain("shared credential store");
        body.Should().NotContain("outbox");

        using var document = JsonDocument.Parse(body);
        var forbidden = document.RootElement.GetProperty("paths").GetProperty("/entities").GetProperty("get")
            .GetProperty("responses").GetProperty("403").GetProperty("description").GetString();
        forbidden.Should().Be("The caller is authenticated but not authorized on this API.");
    }

    /// <summary>
    /// Verifies the healthcheck is reachable anonymously and reports a healthy JSON body.
    /// </summary>
    /// <remarks>
    /// Mirrors the CmsWebhook contract (change add-healthcheck-and-openapi): <c>200 OK</c> with a JSON
    /// body <c>{"status":"Healthy"}</c>, so a blank or malformed body is a regression. Source business
    /// rule: spec "Users API authentication and roles", scenario "Anonymous discovery endpoints";
    /// task 6.1 (anonymous <c>/health</c>).
    /// </remarks>
    [Fact]
    public async Task GetHealth_WithoutAuthorization_ReturnsHealthyJson()
    {
        using var factory = new UsersApiFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/health");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/json");
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        document.RootElement.GetProperty("status").GetString().Should().Be("Healthy");
    }

    /// <summary>
    /// Verifies the Scalar API reference UI is served anonymously in non-Development environments.
    /// </summary>
    /// <remarks>
    /// Source business rule: task 6.1 — always-on Scalar mirroring the CmsWebhook pattern; the test host
    /// is switched to Production so the environment guard (were one reintroduced) cannot make the test
    /// pass vacuously.
    /// </remarks>
    [Fact]
    public async Task GetScalarUi_InProductionEnvironment_ReturnsOkAnonymously()
    {
        using var factory = new UsersApiFactory()
            .WithWebHostBuilder(builder => builder.UseEnvironment("Production"));
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/scalar/v1");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("text/html");
        (await response.Content.ReadAsStringAsync()).Should().NotBeEmpty();
    }

    /// <summary>
    /// Verifies the Scalar UI remains reachable in the default Development environment.
    /// </summary>
    /// <remarks>
    /// Source business rule: the UI is always-on in every environment (change openapi-consumer-ui).
    /// </remarks>
    [Fact]
    public async Task GetScalarUi_InDevelopmentEnvironment_ReturnsOk()
    {
        using var factory = new UsersApiFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/scalar/v1");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("text/html");
    }
}
