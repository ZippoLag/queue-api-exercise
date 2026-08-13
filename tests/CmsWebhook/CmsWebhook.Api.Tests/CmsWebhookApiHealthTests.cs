using System.Net;
using System.Text.Json;
using FluentAssertions;

namespace CmsWebhook.Api.Tests;

/// <summary>
/// Integration tests for the anonymous liveness healthcheck at <c>/health</c>.
/// </summary>
public class CmsWebhookApiHealthTests
{
    /// <summary>
    /// Verifies the healthcheck is reachable anonymously and reports a healthy JSON body.
    /// </summary>
    /// <remarks>
    /// Source business rule: spec "Healthcheck endpoint", scenario "Anonymous liveness probe". The
    /// anonymous carve-out not leaking to protected endpoints is covered by
    /// <see cref="CmsWebhookApiAuthTests"/> and <see cref="CmsWebhookApiOpenApiTests"/>.
    /// </remarks>
    [Fact]
    public async Task GetHealth_WithoutAuthorization_ReturnsHealthyJson()
    {
        using var factory = new CmsWebhookApiFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/health");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/json");
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        document.RootElement.GetProperty("status").GetString().Should().Be("Healthy");
    }
}
