using System.Text;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;

namespace CmsWebhook.Api.Tests;

/// <summary>
/// Unit tests for the <see cref="Program"/> startup configuration helpers exposed for testability.
/// </summary>
/// <remarks>
/// The integration tests cover the happy path (relative appsettings data sources resolve against the
/// repository root on every factory start); these tests pin the remaining branches: absolute and
/// in-memory data sources pass through untouched, and a content root outside the repository falls back
/// to the working directory with a warning.
/// </remarks>
public class ProgramConfigurationTests
{
    /// <summary>
    /// Verifies a missing connection string fails fast with the config key named in the message.
    /// </summary>
    /// <remarks>
    /// Source business rule: both stores' connection strings are mandatory startup configuration (design
    /// D2/D3); a missing key is a setup error that must fail with guidance, never default silently.
    /// </remarks>
    [Theory]
    [InlineData("AuthDb")]
    [InlineData("CmsDb")]
    public void ResolveConnectionString_WithMissingKey_ThrowsNamingTheKey(string key)
    {
        var config = new ConfigurationBuilder().Build();

        var act = () => Program.ResolveConnectionString(config, Path.GetTempPath(), key);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage($"*Missing required configuration 'ConnectionStrings:{key}'*");
    }

    /// <summary>
    /// Verifies an absolute data source is returned unchanged.
    /// </summary>
    /// <remarks>
    /// Absolute paths are the canonical non-repo deployment form (design: "Configure an absolute path
    /// for non-repo deployments"); nothing may be prepended to them.
    /// </remarks>
    [Fact]
    public void ResolveConnectionString_WithAbsoluteDataSource_ReturnsUnchanged()
    {
        var connectionString = $"Data Source={Path.Combine(Path.GetTempPath(), "queue-absolute.db")}";
        var config = ConfigWithConnectionString("TestDb", connectionString);

        var result = Program.ResolveConnectionString(config, Path.GetTempPath(), "TestDb");

        result.Should().Be(connectionString);
    }

    /// <summary>
    /// Verifies an in-memory data source is returned unchanged.
    /// </summary>
    /// <remarks>
    /// <c>:memory:</c> is a special SQLite data source that must not be resolved against the repository
    /// root; it is used by tests and ephemeral tooling.
    /// </remarks>
    [Fact]
    public void ResolveConnectionString_WithInMemoryDataSource_ReturnsUnchanged()
    {
        var config = ConfigWithConnectionString("TestDb", "Data Source=:memory:");

        var result = Program.ResolveConnectionString(config, Path.GetTempPath(), "TestDb");

        result.Should().Be("Data Source=:memory:");
    }

    /// <summary>
    /// Verifies a relative data source is resolved against the repository root, regardless of the content root.
    /// </summary>
    /// <remarks>
    /// Source business rule: the documented "run from the repo root" flow must work from any working
    /// directory; the repository marker <c>QueueApi.slnx</c> anchors the walk (design D2/D3).
    /// </remarks>
    [Fact]
    public void ResolveConnectionString_WithRelativeDataSource_ResolvesAgainstRepositoryRoot()
    {
        var repositoryRoot = FindRepositoryRoot();
        var contentRoot = Path.Combine(repositoryRoot, "src", "CmsWebhook", "CmsWebhook.Api");
        var config = ConfigWithConnectionString("TestDb", "Data Source=db/queue-test.db");

        var result = Program.ResolveConnectionString(config, contentRoot, "TestDb");

        new SqliteConnectionStringBuilder(result).DataSource
            .Should().Be(Path.Combine(repositoryRoot, "db", "queue-test.db"));
    }

    /// <summary>
    /// Verifies a relative data source with no repository marker above the content root is left untouched
    /// and surfaces a warning.
    /// </summary>
    /// <remarks>
    /// Published deployments ship without the <c>QueueApi.slnx</c> marker; the relative path then resolves
    /// against the process working directory, so the assumption is surfaced instead of failing silently.
    /// </remarks>
    [Fact]
    public void ResolveConnectionString_WithoutRepositoryRoot_WarnsAndReturnsUnchanged()
    {
        var tempRoot = Directory.CreateTempSubdirectory("queue-api-no-repo-");
        try
        {
            var config = ConfigWithConnectionString("TestDb", "Data Source=relative/queue.db");
            var previousError = Console.Error;
            using var capturedError = new StringWriter();
            Console.SetError(capturedError);
            try
            {
                var result = Program.ResolveConnectionString(config, tempRoot.FullName, "TestDb");

                result.Should().Be("Data Source=relative/queue.db");
            }
            finally
            {
                Console.SetError(previousError);
            }

            capturedError.ToString().Should().Contain("Could not locate the repository root");
        }
        finally
        {
            tempRoot.Delete(recursive: true);
        }
    }

    private static IConfiguration ConfigWithConnectionString(string name, string value)
        => new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"ConnectionStrings:{name}"] = value,
            })
            .Build();

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "QueueApi.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Test must run inside the repository to locate QueueApi.slnx.");
    }
}
