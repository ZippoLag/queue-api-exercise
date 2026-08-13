using System.Data.Common;
using System.Security.Claims;
using System.Text.Json;
using CmsWebhook.Application;
using CmsWebhook.Domain;
using CmsWebhook.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Data.Sqlite;
using QueueApi.Auth;

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

app.MapGet("/", () => "Hello World!");

app.MapPost("/cms/events", async (
    HttpRequest request,
    IIngestCmsEventsCommandHandler handler,
    CancellationToken cancellationToken) =>
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
});

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
    /// Resolves a connection string, turning relative SQLite data sources into repository-root paths so
    /// the documented "run from the repo root" flow works from any working directory.
    /// </summary>
    /// <remarks>
    /// Serves both stores: the shared credential store (<c>ConnectionStrings:AuthDb</c>) and the dedicated
    /// CMS database (<c>ConnectionStrings:CmsDb</c>, design D3). A single configuration value per store is
    /// the knob for pointing at another location (or, later, another engine via an EF Core provider swap).
    /// Absolute and in-memory data sources are returned unchanged.
    /// </remarks>
    /// <param name="configuration">The application configuration.</param>
    /// <param name="contentRootPath">The host content root, used to locate the repository root.</param>
    /// <param name="connectionStringName">The <c>ConnectionStrings</c> key to read, e.g. <c>AuthDb</c> or <c>CmsDb</c>.</param>
    /// <returns>The connection string, with relative data sources resolved against the repository root.</returns>
    /// <exception cref="InvalidOperationException">
    /// <c>ConnectionStrings:{connectionStringName}</c> is not configured.
    /// </exception>
    private static string ResolveConnectionString(
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

        var repositoryRoot = FindRepositoryRoot(contentRootPath);
        if (repositoryRoot is null)
        {
            // Published deployments ship without the QueueApi.slnx marker; the relative path then resolves
            // against the process working directory, so surface the assumption instead of failing silently.
            Console.Error.WriteLine(
                "[Warning] Could not locate the repository root (QueueApi.slnx) to resolve the relative "
                + $"database path '{builder.DataSource}'; it will be resolved against the working "
                + "directory. Configure an absolute path for non-repo deployments.");
            return connectionString;
        }

        builder.DataSource = Path.GetFullPath(Path.Combine(repositoryRoot, builder.DataSource));
        return builder.ToString();
    }

    /// <summary>
    /// Walks up from <paramref name="startPath"/> looking for the repository marker file <c>QueueApi.slnx</c>.
    /// </summary>
    /// <param name="startPath">The directory where the walk starts.</param>
    /// <returns>The repository root directory, or <see langword="null"/> when the marker is not found.</returns>
    private static string? FindRepositoryRoot(string startPath)
    {
        var directory = new DirectoryInfo(startPath);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "QueueApi.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        return null;
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
