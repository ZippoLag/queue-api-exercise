using System.Data.Common;
using System.Security.Claims;
using CmsWebhook.Api.Endpoints;
using CmsWebhook.Application;
using CmsWebhook.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Data.Sqlite;
using Microsoft.OpenApi.Models;
using QueueApi.Auth;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

var authDbConnectionString = ResolveConnectionString(builder.Configuration, builder.Environment.ContentRootPath, "AuthDb");
var cmsDbConnectionString = ResolveConnectionString(builder.Configuration, builder.Environment.ContentRootPath, "CmsDb");
var cmsUsername = ResolveCmsUsername(builder.Configuration);

builder.Services.AddBasicAuthentication(authDbConnectionString);
builder.Services.AddCmsWebhookInfrastructure(cmsDbConnectionString);
builder.Services.AddScoped<IIngestCmsEventsCommandHandler, IngestCmsEventsCommandHandler>();

builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .RequireClaim(ClaimTypes.Name, cmsUsername)
        .Build();
});

builder.Services.AddOpenApi(options =>
{
    options.AddDocumentTransformer((document, _, _) =>
    {
        document.Info.Title = "CMS Webhook API";
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
            Description = "HTTP Basic authentication against the shared credential store.",
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

        // The ingestion contract (request body oneOf, true status codes) lives on the endpoint's
        // ConfigureOpenApiOperation helper: the handler parses HttpRequest.Body manually and returns
        // IResult, so the generator cannot infer the request schema or the real status codes (change
        // improve-api-documentation).
        if (document.Paths.TryGetValue("/cms/events", out var eventsPath)
            && eventsPath.Operations.TryGetValue(OperationType.Post, out var eventsOperation))
        {
            CmsEventEndpoints.ConfigureOpenApiOperation(eventsOperation);
        }

        // A relative server URL resolves against the origin the document is fetched from, so the contract
        // never advertises a hardcoded http:// scheme for the TLS-only deployment.
        document.Servers = new List<OpenApiServer> { new() { Url = "/" } };

        return Task.CompletedTask;
    });
});
builder.Services.AddHealthChecks();

var app = builder.Build();

// Fail fast: both stores must be reachable, otherwise every request (or the outbox worker) would fail at
// runtime. Surfaces setup problems as descriptive startup errors.
using (var scope = app.Services.CreateScope())
{
    var credentialsProvider = scope.ServiceProvider.GetRequiredService<IUserCredentialsProvider>();
    if (!await credentialsProvider.UserExistsAsync(cmsUsername))
    {
        throw new InvalidOperationException(
            $"The credential store does not contain the cms user '{cmsUsername}'. "
            + "Run 'scripts/init-db.sh' from the repository root to initialize it.");
    }

    var cmsDbContext = scope.ServiceProvider.GetRequiredService<CmsDbContext>();
    await EnsureCmsDatabaseAsync(cmsDbContext, cmsDbConnectionString);
}

app.UseAuthentication();
app.UseAuthorization();

app.MapHealthEndpoints();
app.MapCmsEventEndpoints();
app.MapOpenApi().AllowAnonymous();

// The browsable UI renders the same public contract JSON already served anonymously at /openapi/v1.json,
// so it is served in every environment (design: always-on Scalar, change openapi-consumer-ui).
app.MapScalarApiReference().AllowAnonymous();

app.Run();

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
    /// Serves both stores: the shared credential store (<c>ConnectionStrings:AuthDb</c>) and the dedicated
    /// CMS database (<c>ConnectionStrings:CmsDb</c>, design D3). A single configuration value per store is
    /// the knob for pointing at another location (or, later, another engine via an EF Core provider swap).
    /// Absolute and in-memory data sources are returned unchanged; a relative base path resolves against
    /// the content root. The resolved directory is created when missing so a fresh checkout or deployment
    /// can open its stores without a pre-existing <c>db/</c> directory (spec "Database base directory is
    /// explicit").
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
    /// Resolves the reserved cms username from configuration, enforcing the architecture's username
    /// <c>[10,20]</c> length rule.
    /// </summary>
    /// <remarks>
    /// The username identifies which user in the store the authorization policy allows. It is read
    /// exclusively from <c>Auth:CmsUsername</c> (default <c>cms-webhook</c>), the single source of truth
    /// for the reserved username.
    /// </remarks>
    /// <param name="configuration">The application configuration.</param>
    /// <returns>The resolved cms username.</returns>
    /// <exception cref="InvalidOperationException">
    /// The configured username is shorter than 10 or longer than 20 characters.
    /// </exception>
    private static string ResolveCmsUsername(IConfiguration configuration)
    {
        var username = configuration["Auth:CmsUsername"] ?? "cms-webhook";

        if (username.Length is < 10 or > 20)
        {
            throw new InvalidOperationException(
                $"Configured cms username '{username}' is {username.Length} characters long; "
                + "it must be between 10 and 20 characters (architecture: username [10,20]).");
        }

        return username;
    }

    /// <summary>
    /// Ensures the CMS database exists with its schema, failing fast with descriptive guidance otherwise.
    /// </summary>
    /// <remarks>
    /// Design D8: no migrations tooling is a standing convention, so the schema is created at startup via
    /// <c>EnsureCreated()</c>; there is nothing to seed in the CMS database. WAL journal mode is enabled so
    /// the endpoint's writes and the outbox worker's writes coexist on SQLite's single-writer file (the
    /// busy timeout comes from the connection string, design D3).
    /// </remarks>
    /// <param name="cmsDbContext">The CMS database context.</param>
    /// <param name="cmsDbConnectionString">The resolved CMS database connection string.</param>
    /// <exception cref="InvalidOperationException">
    /// The CMS database could not be accessed or created.
    /// </exception>
    private static async Task EnsureCmsDatabaseAsync(CmsDbContext cmsDbContext, string cmsDbConnectionString)
    {
        try
        {
            await cmsDbContext.Database.EnsureCreatedAsync();
        }
        catch (DbException exception)
        {
            throw new InvalidOperationException(
                "The CMS database could not be accessed. Make sure 'ConnectionStrings:CmsDb' points at a "
                + "writable location; the database file and schema are created automatically at startup.",
                exception);
        }

        await using var connection = new SqliteConnection(cmsDbConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA journal_mode=WAL;";
        await command.ExecuteNonQueryAsync();
    }

}
