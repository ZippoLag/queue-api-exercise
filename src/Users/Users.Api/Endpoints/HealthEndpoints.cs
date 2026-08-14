using System.Text.Json;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Users.Api.Endpoints;

/// <summary>
/// Registers the Users API's health endpoints.
/// </summary>
/// <remarks>
/// Mirrors the CmsWebhook pattern (change add-healthcheck-and-openapi): health is a cross-cutting,
/// feature-level concern with a home of its own so <c>Program.cs</c> stays about composition. The probe
/// is anonymous so load balancers and orchestrators can check liveness without credentials.
/// </remarks>
public static class HealthEndpoints
{
    /// <summary>
    /// Maps the anonymous liveness healthcheck at <c>/health</c>.
    /// </summary>
    /// <param name="endpoints">The endpoint route builder to register the endpoint on.</param>
    /// <returns>The endpoint route builder for chaining.</returns>
    public static IEndpointRouteBuilder MapHealthEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapHealthChecks("/health", new HealthCheckOptions
        {
            // The built-in result-status-code mapping (Healthy/Degraded -> 200, Unhealthy -> 503) is kept;
            // this writer only supplies the JSON body the load balancers expect (design decision D1).
            ResponseWriter = WriteJsonResponseAsync,
        }).AllowAnonymous();

        return endpoints;
    }

    /// <summary>
    /// Writes the health report as a JSON body reporting the overall status.
    /// </summary>
    /// <param name="context">The current HTTP context.</param>
    /// <param name="report">The health check report to serialize.</param>
    /// <returns>A task completing once the response body has been written.</returns>
    private static Task WriteJsonResponseAsync(HttpContext context, HealthReport report)
    {
        var status = report.Status == HealthStatus.Healthy ? "Healthy" : "Unhealthy";
        context.Response.ContentType = "application/json";
        return context.Response.WriteAsync(JsonSerializer.Serialize(new { status }));
    }
}
