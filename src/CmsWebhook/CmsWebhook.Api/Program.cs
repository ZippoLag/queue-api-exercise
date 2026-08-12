using Microsoft.AspNetCore.Authorization;
using Microsoft.Data.Sqlite;
using QueueApi.Auth;
using System.Security.Claims;

var builder = WebApplication.CreateBuilder(args);

var authDbConnectionString = ResolveConnectionString(builder.Configuration, builder.Environment.ContentRootPath);
var cmsUsername = ResolveCmsUsername(builder.Configuration);

builder.Services.AddBasicAuthentication(authDbConnectionString);

builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .RequireClaim(ClaimTypes.Name, cmsUsername)
        .Build();
});

var app = builder.Build();

// Fail fast: the credential store must be reachable and initialized with the cms user, otherwise
// every request would 401 at runtime. This surfaces setup problems as a descriptive startup error.
using (var scope = app.Services.CreateScope())
{
    var credentialsProvider = scope.ServiceProvider.GetRequiredService<IUserCredentialsProvider>();
    if (!await credentialsProvider.UserExistsAsync(cmsUsername))
    {
        throw new InvalidOperationException(
            $"The credential store does not contain the cms user '{cmsUsername}'. "
            + "Run 'scripts/init-db.sh' from the repository root to initialize it.");
    }
}

app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/", () => "Hello World!");

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
    /// Resolves the credential store connection string, turning relative SQLite data sources into
    /// repository-root paths so the documented "run from the repo root" flow works from any working directory.
    /// </summary>
    /// <remarks>
    /// Spec "Credential store location is configurable": a single configuration value is the knob for
    /// pointing at another store (or, later, another engine via an EF Core provider swap — design decision D5).
    /// Absolute and in-memory data sources are returned unchanged.
    /// </remarks>
    /// <param name="configuration">The application configuration.</param>
    /// <param name="contentRootPath">The host content root, used to locate the repository root.</param>
    /// <returns>The connection string, with relative data sources resolved against the repository root.</returns>
    /// <exception cref="InvalidOperationException">
    /// <c>ConnectionStrings:AuthDb</c> is not configured.
    /// </exception>
    private static string ResolveConnectionString(IConfiguration configuration, string contentRootPath)
    {
        var connectionString = configuration.GetConnectionString("AuthDb")
            ?? throw new InvalidOperationException(
                "Missing required configuration 'ConnectionStrings:AuthDb'. "
                + "The credential store connection string must be configured.");

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
                + $"credential store path '{builder.DataSource}'; it will be resolved against the working "
                + "directory. Configure an absolute path in ConnectionStrings:AuthDb for non-repo deployments.");
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
    /// The username no longer comes from the credential provider (design decision D4); it identifies which
    /// user in the store the authorization policy allows. It is read exclusively from <c>Auth:CmsUsername</c>
    /// (default <c>cms-webhook</c>), the single source of truth for the reserved username.
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
}
