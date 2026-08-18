using System.Security.Claims;
using Microsoft.OpenApi.Models;
using Users.Application;

namespace Users.Api.Endpoints;

/// <summary>
/// Registers the Users API's entity endpoints.
/// </summary>
/// <remarks>
/// Mirrors the CmsWebhook organization: each feature owns its endpoint mapping and OpenAPI metadata. The
/// fallback authorization policy (authenticated, non-<c>cms-webhook</c>) protects <c>GET /entities</c>;
/// the administrator-only policy protects the enable/disable commands (design decision 1 of the
/// users-api-vertical change). The administrator's role for the listing is resolved from the principal
/// against the configured administrator username, keeping concrete usernames out of the Application layer.
/// The OpenAPI contract is declared in <see cref="ConfigureListOperation"/> and
/// <see cref="ConfigureSetVisibilityOperation"/>, invoked from the <c>AddOpenApi</c> document transformer
/// (change improve-api-documentation): operation-level <c>WithOpenApi</c> callbacks are not applied by the
/// 9.0.18 generator, so the contract is set where it is guaranteed to run.
/// </remarks>
public static class EntityEndpoints
{
    /// <summary>
    /// The authorization policy allowing only the administrator to enable/disable entity visibility.
    /// </summary>
    public const string AdministratorPolicy = "AdministratorOnly";

    /// <summary>
    /// Maps the entity listing and enable/disable endpoints.
    /// </summary>
    /// <param name="endpoints">The endpoint route builder to register the endpoints on.</param>
    /// <returns>The endpoint route builder for chaining.</returns>
    public static IEndpointRouteBuilder MapEntityEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/entities", ListAsync)
            .WithSummary("List published entities")
            .WithDescription(
                "Returns the currently published entities: the administrator sees all of them, a regular "
                + "user only those not disabled by an administrator. Each item carries the entity id, its "
                + "administrator-visibility flag, latest version, last update time and payload.")
            .WithTags("entities");

        endpoints.MapPost("/entities/{id}/disable", DisableAsync)
            .RequireAuthorization(AdministratorPolicy)
            .WithSummary("Disable an entity")
            .WithDescription(
                "Hides the entity from regular users' listings. Idempotent; no request body; "
                + "204 on success, 404 for an unknown entity id.")
            .WithTags("entities");

        endpoints.MapPost("/entities/{id}/enable", EnableAsync)
            .RequireAuthorization(AdministratorPolicy)
            .WithSummary("Enable an entity")
            .WithDescription(
                "Restores the entity to regular users' listings. Idempotent; no request body; "
                + "204 on success, 404 for an unknown entity id.")
            .WithTags("entities");

        return endpoints;
    }

    /// <summary>
    /// Declares the response contract of <c>GET /entities</c>: the item schema and its status codes.
    /// </summary>
    /// <remarks>
    /// The handler returns <c>IResult</c>, so the response shape is declared explicitly; property names
    /// match the default System.Text.Json serialization (PascalCase) of <c>EntityListItem</c>. The
    /// authorization layer adds 401 (missing credentials) and 403 (valid credentials of a user not
    /// authorized on this API), so the generated default 200-only contract would lie about the failure
    /// modes.
    /// </remarks>
    /// <param name="operation">The generated OpenAPI operation to correct; mutated in place.</param>
    public static void ConfigureListOperation(OpenApiOperation operation)
    {
        var itemSchema = new OpenApiSchema
        {
            Type = "object",
            Required = new HashSet<string> { "Id", "IsVisibleByAdmin", "LatestVersion", "UpdatedAt", "Payload" },
            Properties = new Dictionary<string, OpenApiSchema>
            {
                ["Id"] = new OpenApiSchema { Type = "string", Description = "The external entity's id." },
                ["IsVisibleByAdmin"] = new OpenApiSchema
                {
                    Type = "boolean",
                    Description = "Whether an administrator has disabled the entity for regular users.",
                },
                ["LatestVersion"] = new OpenApiSchema
                {
                    Type = "integer",
                    Format = "int32",
                    Description = "The latest known data version.",
                },
                ["UpdatedAt"] = new OpenApiSchema
                {
                    Type = "string",
                    Format = "date-time",
                    Description = "When the latest version was last updated.",
                },
                ["Payload"] = new OpenApiSchema
                {
                    Type = "object",
                    Description = "The latest payload as a raw JSON object.",
                },
            },
        };

        operation.Responses = new OpenApiResponses
        {
            ["200"] = new OpenApiResponse
            {
                Description = "The published entities visible to the caller (all for the administrator, enabled only for regular users).",
                Content = new Dictionary<string, OpenApiMediaType>
                {
                    ["application/json"] = new OpenApiMediaType
                    {
                        Schema = new OpenApiSchema { Type = "array", Items = itemSchema },
                        Example = new Microsoft.OpenApi.Any.OpenApiArray
                        {
                            new Microsoft.OpenApi.Any.OpenApiObject
                            {
                                ["Id"] = new Microsoft.OpenApi.Any.OpenApiString("entity-1"),
                                ["IsVisibleByAdmin"] = new Microsoft.OpenApi.Any.OpenApiBoolean(true),
                                ["LatestVersion"] = new Microsoft.OpenApi.Any.OpenApiInteger(1),
                                ["UpdatedAt"] = new Microsoft.OpenApi.Any.OpenApiString("2024-01-01T00:00:00Z"),
                                ["Payload"] = new Microsoft.OpenApi.Any.OpenApiObject
                                {
                                    ["title"] = new Microsoft.OpenApi.Any.OpenApiString("hello"),
                                },
                            },
                        },
                    },
                },
            },
            ["401"] = new OpenApiResponse { Description = "Missing or invalid credentials." },
            ["403"] = new OpenApiResponse { Description = "The caller is authenticated but not authorized on this API." },
        };
    }

    /// <summary>
    /// Declares the shared response contract of the administrator-only enable/disable commands.
    /// </summary>
    /// <remarks>
    /// Both handlers return <c>IResult</c> with 204/404, and the authorization layer adds 401/403; the
    /// generated default 200 would lie about the contract, so it is replaced explicitly.
    /// </remarks>
    /// <param name="operation">The generated OpenAPI operation to correct; mutated in place.</param>
    public static void ConfigureSetVisibilityOperation(OpenApiOperation operation)
    {
        operation.Responses = new OpenApiResponses
        {
            ["204"] = new OpenApiResponse { Description = "The visibility change was applied (idempotent)." },
            ["401"] = new OpenApiResponse { Description = "Missing or invalid credentials." },
            ["403"] = new OpenApiResponse { Description = "The caller is not the administrator." },
            ["404"] = new OpenApiResponse { Description = "No entity with this id is known." },
        };
    }

    /// <summary>
    /// Handles <c>GET /entities</c>: lists the published entities visible to the caller.
    /// </summary>
    /// <param name="httpContext">The current HTTP context, whose principal carries the caller's username.</param>
    /// <param name="roles">The reserved usernames this API recognizes.</param>
    /// <param name="handler">The query handler applying the visibility rule.</param>
    /// <param name="cancellationToken">The request cancellation token.</param>
    /// <returns><c>200 OK</c> with the visible entities.</returns>
    private static async Task<IResult> ListAsync(
        HttpContext httpContext,
        UsersApiRoles roles,
        IListEntitiesQueryHandler handler,
        CancellationToken cancellationToken)
    {
        var isAdministrator = string.Equals(
            httpContext.User.FindFirstValue(ClaimTypes.Name),
            roles.AdministratorUsername,
            StringComparison.Ordinal);

        var items = await handler.HandleAsync(new ListEntitiesQuery(isAdministrator), cancellationToken);
        return Results.Ok(items);
    }

    /// <summary>
    /// Handles <c>POST /entities/{id}/disable</c>: hides the entity from regular users.
    /// </summary>
    /// <param name="id">The entity id from the route.</param>
    /// <param name="handler">The command handler applying the visibility change.</param>
    /// <param name="cancellationToken">The request cancellation token.</param>
    /// <returns><c>204 No Content</c> on success, <c>404 Not Found</c> for an unknown id.</returns>
    private static async Task<IResult> DisableAsync(
        string id,
        ISetEntityVisibilityCommandHandler handler,
        CancellationToken cancellationToken)
    {
        var updated = await handler.HandleAsync(new SetEntityVisibilityCommand(id, IsVisibleByAdmin: false), cancellationToken);
        return updated ? Results.NoContent() : Results.NotFound();
    }

    /// <summary>
    /// Handles <c>POST /entities/{id}/enable</c>: restores the entity to regular users.
    /// </summary>
    /// <param name="id">The entity id from the route.</param>
    /// <param name="handler">The command handler applying the visibility change.</param>
    /// <param name="cancellationToken">The request cancellation token.</param>
    /// <returns><c>204 No Content</c> on success, <c>404 Not Found</c> for an unknown id.</returns>
    private static async Task<IResult> EnableAsync(
        string id,
        ISetEntityVisibilityCommandHandler handler,
        CancellationToken cancellationToken)
    {
        var updated = await handler.HandleAsync(new SetEntityVisibilityCommand(id, IsVisibleByAdmin: true), cancellationToken);
        return updated ? Results.NoContent() : Results.NotFound();
    }
}
