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
    /// Verifies the healthcheck is served anonymously.
    /// </summary>
    /// <remarks>
    /// Source business rule: spec "Users API authentication and roles", scenario
    /// "Anonymous discovery endpoints"; task 6.1 (anonymous <c>/health</c>).
    /// </remarks>
    [Fact]
    public async Task GetHealth_WithoutAuthorization_ReturnsOk()
    {
        using var factory = new UsersApiFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/health");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("Healthy");
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
