using System.Text.Json;
using CmsWebhook.Application;
using CmsWebhook.Domain;

namespace CmsWebhook.Api.Endpoints;

/// <summary>
/// Registers the CMS event ingestion endpoints.
/// </summary>
/// <remarks>
/// The <c>/cms/events</c> handler moved here from <c>Program.cs</c> so each feature owns its endpoint
/// mapping and its OpenAPI metadata (design decision D2 of change add-healthcheck-and-openapi). The
/// endpoint's behavior is unchanged; this is a behavior-neutral reorganization.
/// </remarks>
public static class CmsEventEndpoints
{
    /// <summary>
    /// Maps the <c>POST /cms/events</c> ingestion endpoint.
    /// </summary>
    /// <param name="endpoints">The endpoint route builder to register the endpoint on.</param>
    /// <returns>The endpoint route builder for chaining.</returns>
    public static IEndpointRouteBuilder MapCmsEventEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/cms/events", IngestAsync)
            .WithSummary("Ingest CMS events")
            .WithDescription("Accepts a single CMS event or a batch of events, records them in the outbox, and returns 201 when accepted.")
            .WithTags("cms-events");

        return endpoints;
    }

    /// <summary>
    /// Handles the <c>POST /cms/events</c> request: parses the body, deserializes and validates it, and
    /// records the accepted events before responding.
    /// </summary>
    /// <param name="request">The HTTP request carrying the JSON body.</param>
    /// <param name="handler">The command handler that records the accepted events.</param>
    /// <param name="cancellationToken">The request cancellation token.</param>
    /// <returns><c>201 Created</c> on acceptance, otherwise <c>400 Bad Request</c>.</returns>
    private static async Task<IResult> IngestAsync(
        HttpRequest request,
        IIngestCmsEventsCommandHandler handler,
        CancellationToken cancellationToken)
    {
        JsonDocument? document = null;
        try
        {
            document = await JsonDocument.ParseAsync(request.Body, cancellationToken: cancellationToken);
        }
        catch (JsonException)
        {
            return Results.BadRequest();
        }

        // The document must stay alive while requests are deserialized and validated: CmsRequest payloads
        // are JsonElements referencing its memory, and the validator reads them via GetRawText().
        using (document)
        {
            IReadOnlyList<CmsRequest?>? requests;
            try
            {
                var root = document.RootElement;
                requests = root.ValueKind switch
                {
                    JsonValueKind.Object => new[] { DeserializeCmsRequest(root) },
                    JsonValueKind.Array => DeserializeCmsRequestBatch(root),
                    _ => null,
                };
            }
            catch (JsonException)
            {
                // Unparseable or wrongly-typed fields (e.g. "type": 5) are client errors, not server errors.
                return Results.BadRequest();
            }

            if (requests is null || requests.Any(item => item is null))
            {
                return Results.BadRequest();
            }

            var result = await handler.HandleAsync(requests.Cast<CmsRequest>().ToList(), cancellationToken);
            return result.Success ? Results.StatusCode(StatusCodes.Status201Created) : Results.BadRequest();
        }
    }

    /// <summary>
    /// Deserializes a single CMS request from the parsed JSON body.
    /// </summary>
    /// <param name="element">The JSON object to deserialize.</param>
    /// <returns>The deserialized request, or <see langword="null"/> when the element is null.</returns>
    private static CmsRequest? DeserializeCmsRequest(JsonElement element)
        => element.Deserialize<CmsRequest>();

    /// <summary>
    /// Deserializes a batch of CMS requests from the parsed JSON body.
    /// </summary>
    /// <param name="element">The JSON array to deserialize.</param>
    /// <returns>The deserialized requests; null elements (invalid batch members) are kept for validation.</returns>
    private static IReadOnlyList<CmsRequest?>? DeserializeCmsRequestBatch(JsonElement element)
        => element.Deserialize<List<CmsRequest?>>();
}
