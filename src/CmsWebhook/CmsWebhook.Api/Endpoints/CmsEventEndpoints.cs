using System.Text.Json;
using CmsWebhook.Application;
using CmsWebhook.Domain;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;

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
    /// The fixed-window rate-limit policy applied to the ingestion endpoint, registered in
    /// <c>Program.cs</c> from <c>RateLimiting:PermitLimit</c>/<c>RateLimiting:WindowSeconds</c>.
    /// </summary>
    /// <remarks>
    /// Only ingestion is rate limited: the anonymous discovery endpoints and the liveness probe stay
    /// exempt, and the rate limiter runs before authentication so unauthenticated floods are rejected
    /// with 429 without touching the credential store (spec: rate-limiting).
    /// </remarks>
    public const string IngestionRateLimitPolicy = "ingestion";

    /// <summary>
    /// Maps the <c>POST /cms/events</c> ingestion endpoint.
    /// </summary>
    /// <param name="endpoints">The endpoint route builder to register the endpoint on.</param>
    /// <returns>The endpoint route builder for chaining.</returns>
    public static IEndpointRouteBuilder MapCmsEventEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/cms/events", IngestAsync)
            .RequireRateLimiting(IngestionRateLimitPolicy)
            .WithSummary("Ingest CMS events")
            .WithDescription("Accepts a single CMS event or a batch of events, records them for processing, and returns 201 when accepted.")
            .WithTags("cms-events");

        return endpoints;
    }

    /// <summary>
    /// Corrects the generated OpenAPI operation for <c>POST /cms/events</c>.
    /// </summary>
    /// <remarks>
    /// The handler parses <c>HttpRequest.Body</c> manually, so the generator infers no request schema and
    /// emits a default 200 response. Both accepted forms (a single object or a batch array) are declared as
    /// a <c>oneOf</c> so the contract matches the verified runtime behavior, and the responses are replaced
    /// with the actual status codes. Called from the <c>AddOpenApi</c> document transformer (change
    /// improve-api-documentation): operation-level <c>WithOpenApi</c> callbacks were not applied by the
    /// 9.0.18 generator, so the contract is set in the document transformer where it is guaranteed to run.
    /// </remarks>
    /// <param name="operation">The generated operation to correct; mutated in place.</param>
    public static void ConfigureOpenApiOperation(OpenApiOperation operation)
    {
        // payload/version are required except for delete events — that type-dependent rule cannot be
        // expressed in one schema, so it lives in the descriptions below.
        var eventSchema = new OpenApiSchema
        {
            Type = "object",
            Required = new HashSet<string> { "type", "id", "timestamp" },
            Properties = new Dictionary<string, OpenApiSchema>
            {
                ["type"] = new OpenApiSchema
                {
                    Type = "string",
                    Description = "The operation performed upon the entity: publish, update, unPublish or delete (case-sensitive).",
                    Enum = new List<IOpenApiAny>
                    {
                        new OpenApiString("publish"),
                        new OpenApiString("update"),
                        new OpenApiString("unPublish"),
                        new OpenApiString("delete"),
                    },
                },
                ["id"] = new OpenApiSchema { Type = "string", Description = "The external entity's id." },
                ["payload"] = new OpenApiSchema
                {
                    Type = "object",
                    Nullable = true,
                    Description = "The entity data as a JSON object; required for every type except delete.",
                },
                ["version"] = new OpenApiSchema
                {
                    Type = "integer",
                    Format = "int32",
                    Minimum = 1,
                    Nullable = true,
                    Description = "The entity's version from the external system (the first version is 1); required for every type except delete.",
                },
                ["timestamp"] = new OpenApiSchema
                {
                    Type = "string",
                    Format = "date-time",
                    Description = "ISO 8601 / RFC 3339 date-time of when the event happened in the external CMS.",
                },
            },
        };

        operation.RequestBody = new OpenApiRequestBody
        {
            Required = true,
            Description = "A single CMS event object or a batch array of event objects.",
            Content = new Dictionary<string, OpenApiMediaType>
            {
                ["application/json"] = new OpenApiMediaType
                {
                    Schema = new OpenApiSchema
                    {
                        OneOf =
                        [
                            eventSchema,
                            new OpenApiSchema { Type = "array", Items = eventSchema },
                        ],
                    },
                    Example = new OpenApiObject
                    {
                        ["type"] = new OpenApiString("publish"),
                        ["id"] = new OpenApiString("entity-1"),
                        ["payload"] = new OpenApiObject { ["title"] = new OpenApiString("hello") },
                        ["version"] = new OpenApiInteger(1),
                        ["timestamp"] = new OpenApiString("2024-01-01T00:00:00Z"),
                    },
                },
            },
        };

        operation.Responses = new OpenApiResponses
        {
            ["201"] = new OpenApiResponse
            {
                Description = "The event(s) were validated and recorded for processing; a batch is all-or-nothing (an invalid element rejects the whole batch).",
            },
            ["400"] = new OpenApiResponse
            {
                Description = "The body is not valid JSON, is neither an object nor an array of objects, or fails validation (unknown type, missing/invalid id, version or timestamp).",
            },
            ["401"] = new OpenApiResponse { Description = "Missing or invalid credentials." },
            ["403"] = new OpenApiResponse { Description = "The caller is authenticated but not authorized on this API." },
            ["429"] = new OpenApiResponse { Description = "The caller exceeded the allowed request rate; retry later." },
        };
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
