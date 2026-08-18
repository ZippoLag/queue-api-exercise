using System.Data.Common;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Data.Sqlite;
using Microsoft.OpenApi.Models;
using QueueApi.Auth;
using Scalar.AspNetCore;
using Users.Api.Endpoints;
using Users.Application;
using Users.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

var authDbConnectionString = ResolveConnectionString(builder.Configuration, builder.Environment.ContentRootPath, "AuthDb");
var cmsDbConnectionString = ResolveConnectionString(builder.Configuration, builder.Environment.ContentRootPath, "CmsDb");
var cmsUsername = ResolveUsername(builder.Configuration, "Auth:CmsUsername", "cms-webhook");
var administratorUsername = ResolveUsername(builder.Configuration, "Auth:AdministratorUsername", "administrator");

builder.Services.AddBasicAuthentication(authDbConnectionString, builder.Configuration);
builder.Services.AddUsersInfrastructure(cmsDbConnectionString, builder.Configuration);
builder.Services.AddScoped<IListEntitiesQueryHandler, ListEntitiesQueryHandler>();
builder.Services.AddScoped<ISetEntityVisibilityCommandHandler, SetEntityVisibilityCommandHandler>();
builder.Services.AddSingleton(new UsersApiRoles(cmsUsername, administratorUsername));

builder.Services.AddAuthorization(options =>
{
    // Fallback: any authenticated user except the cms user (reserved for the CMS Webhook API) may list
    // entities; role filtering inside the listing happens in the query handler. cms-webhook is rejected
    // with 403 by this assertion, matching the spec "cms-webhook is rejected on the Users API".
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .RequireAssertion(context => !string.Equals(
            context.User.FindFirstValue(ClaimTypes.Name),
            cmsUsername,
            StringComparison.Ordinal))
        .Build();

    options.AddPolicy(EntityEndpoints.AdministratorPolicy, policy => policy
        .RequireAuthenticatedUser()
        .RequireClaim(ClaimTypes.Name, administratorUsername)
        .Build());
});

builder.Services.AddOpenApi(options =>
{
    options.AddDocumentTransformer((document, _, _) =>
    {
        document.Info.Title = "Users API";
        document.Info.Version = "v1";

        // MapHealthChecks registers a raw RequestDelegate pipeline with no OpenAPI metadata, so the
        // generator omits it. Declare the liveness probe explicitly so the contract still describes it.
        document.Paths["/health"] = new OpenApiPathItem
        {
            Operations = new Dictionary<OperationType, OpenApiOperation>
            {
                [OperationType.Get] = new OpenApiOperation
                {
                    Summary = "Liveness probe",
                    Tags = new List<OpenApiTag> { new() { Name = "health" } },
                    Responses = new OpenApiResponses
                    {
                        ["200"] = new OpenApiResponse { Description = "The application is healthy." },
                        ["503"] = new OpenApiResponse { Description = "The application is unhealthy." },
                    },
                },
            },
        };

        // Every operation except the anonymous liveness probe requires HTTP Basic authentication; declare
        // the scheme in components and attach it per operation so consumers (and the Scalar UI) see the
        // auth contract instead of an empty components section.
        document.Components ??= new OpenApiComponents();
        var basicScheme = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "basic",
            Description = "HTTP Basic authentication with a valid username and password.",
            Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "basic" },
        };
        document.Components.SecuritySchemes["basic"] = basicScheme;
        foreach (var (path, pathItem) in document.Paths)
        {
            if (path == "/health")
            {
                continue;
            }

            foreach (var operation in pathItem.Operations.Values)
            {
                operation.Security = new List<OpenApiSecurityRequirement> { new() { [basicScheme] = [] } };
            }
        }

        // The entity contracts live on the endpoint's Configure*Operation helpers: the handlers return
        // IResult, so the generator cannot infer the response schemas or the real status codes (change
        // improve-api-documentation).
        if (document.Paths.TryGetValue("/entities", out var entitiesPath)
            && entitiesPath.Operations.TryGetValue(OperationType.Get, out var listOperation))
        {
            EntityEndpoints.ConfigureListOperation(listOperation);
        }

        foreach (var visibilityPathName in new[] { "/entities/{id}/disable", "/entities/{id}/enable" })
        {
            if (document.Paths.TryGetValue(visibilityPathName, out var visibilityPath)
                && visibilityPath.Operations.TryGetValue(OperationType.Post, out var visibilityOperation))
            {
                EntityEndpoints.ConfigureSetVisibilityOperation(visibilityOperation);
            }
        }

        // A relative server URL resolves against the origin the document is fetched from, so the contract
        // never advertises a hardcoded http:// scheme for the TLS-only deployment.
        document.Servers = new List<OpenApiServer> { new() { Url = "/" } };

        return Task.CompletedTask;
    });
});
builder.Services.AddHealthChecks();

var app = builder.Build();

// Fail fast: the store must hold the administrator user, otherwise the admin endpoints and the
// administrator's listing view would be unreachable at runtime. Surfaces setup problems as descriptive
// startup errors (design decision 4 of the users-api-vertical change, mirroring the CmsWebhook check).
using (var scope = app.Services.CreateScope())
{
    var credentialsProvider = scope.ServiceProvider.GetRequiredService<IUserCredentialsProvider>();
    if (!await credentialsProvider.UserExistsAsync(administratorUsername))
    {
        throw new InvalidOperationException(
            $"The credential store does not contain the administrator user '{administratorUsername}'. "
            + "Run 'scripts/init-db.sh' from the repository root to initialize it.");
    }
}

// The hosted Blazor client (change extra-ui): static web assets — the client's wwwroot content and its
// _framework files — must load without credentials so the app can boot and ask for sign-in. The static
// middleware short-circuits before authentication, and the SPA fallback is explicitly anonymous, while
// every mapped endpoint below keeps its exact auth semantics (design D2).
app.UseStaticFiles();
app.UseBlazorFrameworkFiles();

app.UseAuthentication();
app.UseAuthorization();

app.MapHealthEndpoints();
app.MapEntityEndpoints();
app.MapOpenApi().AllowAnonymous();

// The browsable UI renders the same public contract JSON already served anonymously at /openapi/v1.json,
// so it is served in every environment (design: always-on Scalar, change openapi-consumer-ui).
app.MapScalarApiReference().AllowAnonymous();

// Last-resort route: any path no endpoint matched is a client-side route and receives the application
// shell (change extra-ui, spec "Users API hosts a browser UI").
app.MapFallbackToFile("index.html").AllowAnonymous();

app.Run();

/// <summary>
/// The reserved usernames the Users API recognizes, resolved once at startup and injected so both the
/// authorization policies and the endpoints share a single source of truth.
/// </summary>
/// <param name="CmsUsername">The cms username rejected on this API (reserved for the CMS Webhook API).</param>
/// <param name="AdministratorUsername">The username with administrator privileges.</param>
public sealed record UsersApiRoles(string CmsUsername, string AdministratorUsername);

/// <summary>
/// Exposes the web application entry point to integration tests via <c>WebApplicationFactory&lt;Program&gt;</c>.
/// </summary>
/// <remarks>
/// The startup helper methods live on this class rather than as local functions so they can carry XML
/// documentation with <c>&lt;exception&gt;</c> tags: XML doc comments are not valid on local functions
/// (compiler warning CS1587), which is why they were hoisted out of the top-level statements.
/// </remarks>
public partial class Program
{
    /// <summary>
    /// Resolves a connection string, turning relative SQLite data sources into absolute paths against the
    /// configured database base directory (<c>Data:DbBasePath</c>), falling back to the content root.
    /// </summary>
    /// <remarks>
    /// Serves both stores: the shared credential store (<c>ConnectionStrings:AuthDb</c>) and the shared
    /// CMS database (<c>ConnectionStrings:CmsDb</c>). The Users API defaults its base path to the
    /// CmsWebhook project directory (see <c>appsettings.json</c>) so both APIs address the same stores in
    /// local development. Absolute and in-memory data sources are returned unchanged; a relative base path
    /// resolves against the content root. The resolved directory is created when missing so a fresh
    /// checkout or deployment can open its stores without a pre-existing <c>db/</c> directory.
    /// </remarks>
    /// <param name="configuration">The application configuration.</param>
    /// <param name="contentRootPath">
    /// The host content root; the fallback base for relative data sources when <c>Data:DbBasePath</c> is unset.
    /// </param>
    /// <param name="connectionStringName">The <c>ConnectionStrings</c> key to read, e.g. <c>AuthDb</c> or <c>CmsDb</c>.</param>
    /// <returns>
    /// The connection string, with relative data sources resolved against <c>Data:DbBasePath</c> or the content root.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    /// <c>ConnectionStrings:{connectionStringName}</c> is not configured.
    /// </exception>
    internal static string ResolveConnectionString(
        IConfiguration configuration,
        string contentRootPath,
        string connectionStringName)
    {
        var connectionString = configuration.GetConnectionString(connectionStringName)
            ?? throw new InvalidOperationException(
                $"Missing required configuration 'ConnectionStrings:{connectionStringName}'. "
                + "The connection string must be configured.");

        var builder = new SqliteConnectionStringBuilder(connectionString);
        if (string.IsNullOrWhiteSpace(builder.DataSource)
            || builder.DataSource == ":memory:"
            || Path.IsPathRooted(builder.DataSource))
        {
            return connectionString;
        }

        var basePath = configuration["Data:DbBasePath"];
        var baseDirectory = string.IsNullOrWhiteSpace(basePath)
            ? contentRootPath
            : Path.GetFullPath(Path.IsPathRooted(basePath) ? basePath : Path.Combine(contentRootPath, basePath));

        builder.DataSource = Path.Combine(baseDirectory, builder.DataSource);
        Directory.CreateDirectory(Path.GetDirectoryName(builder.DataSource)!);
        return builder.ToString();
    }

    /// <summary>
    /// Resolves a reserved username from configuration, enforcing the architecture's username
    /// <c>[10,20]</c> length rule.
    /// </summary>
    /// <remarks>
    /// Usernames are compared exactly as configured (case-sensitive), matching the store's username
    /// semantics (design decision 1 of the users-api-vertical change). The values default to the reserved
    /// names the initialization script seeds.
    /// </remarks>
    /// <param name="configuration">The application configuration.</param>
    /// <param name="key">The configuration key to read, e.g. <c>Auth:CmsUsername</c>.</param>
    /// <param name="defaultValue">The reserved default (e.g. <c>cms-webhook</c>).</param>
    /// <returns>The resolved username.</returns>
    /// <exception cref="InvalidOperationException">
    /// The configured username is shorter than 10 or longer than 20 characters.
    /// </exception>
    private static string ResolveUsername(IConfiguration configuration, string key, string defaultValue)
    {
        var username = configuration[key] ?? defaultValue;

        if (username.Length is < 10 or > 20)
        {
            throw new InvalidOperationException(
                $"Configured username '{username}' for '{key}' is {username.Length} characters long; "
                + "it must be between 10 and 20 characters (architecture: username [10,20]).");
        }

        return username;
    }
}
