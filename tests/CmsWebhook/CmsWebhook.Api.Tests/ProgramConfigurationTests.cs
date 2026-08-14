using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;

namespace CmsWebhook.Api.Tests;

/// <summary>
/// Unit tests for the <see cref="Program"/> startup configuration helpers exposed for testability.
/// </summary>
/// <remarks>
/// The integration tests cover the happy path (relative appsettings data sources resolve against the
/// content root on every factory start); these tests pin the remaining branches: absolute and in-memory
/// data sources pass through untouched, and relative data sources resolve against
/// <c>Data:DbBasePath</c> (falling back to the content root), creating the target directory when missing.
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
    /// Absolute paths are the canonical deployment form (spec: "Absolute data source is used as-is");
    /// nothing may be prepended to them and no directory is created on their behalf.
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
    /// <c>:memory:</c> is a special SQLite data source that must not be resolved against a base directory;
    /// it is used by tests and ephemeral tooling (spec: "In-memory data source is used as-is").
    /// </remarks>
    [Fact]
    public void ResolveConnectionString_WithInMemoryDataSource_ReturnsUnchanged()
    {
        var config = ConfigWithConnectionString("TestDb", "Data Source=:memory:");

        var result = Program.ResolveConnectionString(config, Path.GetTempPath(), "TestDb");

        result.Should().Be("Data Source=:memory:");
    }

    /// <summary>
    /// Verifies an empty data source is returned unchanged.
    /// </summary>
    /// <remarks>
    /// A connection string with no data source has nothing to resolve; it must pass through untouched
    /// rather than being joined onto the base directory.
    /// </remarks>
    [Fact]
    public void ResolveConnectionString_WithEmptyDataSource_ReturnsUnchanged()
    {
        var config = ConfigWithConnectionString("TestDb", "Data Source=");

        var result = Program.ResolveConnectionString(config, Path.GetTempPath(), "TestDb");

        result.Should().Be("Data Source=");
    }

    /// <summary>
    /// Verifies a relative data source resolves against the configured absolute <c>Data:DbBasePath</c>.
    /// </summary>
    /// <remarks>
    /// Source business rule: spec "Relative data source resolves against the configured base directory" —
    /// the deployment knob that removes the dependency on a repository marker file.
    /// </remarks>
    [Fact]
    public void ResolveConnectionString_WithRelativeDataSourceAndConfiguredBasePath_ResolvesAgainstBasePath()
    {
        using var tempRoot = new TemporaryDirectory("queue-api-base-");
        var config = ConfigWithConnectionString("TestDb", "Data Source=db/queue-test.db", tempRoot.FullName);

        var result = Program.ResolveConnectionString(config, Path.GetTempPath(), "TestDb");

        new SqliteConnectionStringBuilder(result).DataSource
            .Should().Be(Path.Combine(tempRoot.FullName, "db", "queue-test.db"));
    }

    /// <summary>
    /// Verifies a relative <c>Data:DbBasePath</c> resolves against the content root before joining the data source.
    /// </summary>
    /// <remarks>
    /// Relative base paths are resolved against the content root (design "Relative Data:DbBasePath
    /// confusion" risk); this keeps a short base path like <c>databases</c> meaningful from any content root.
    /// </remarks>
    [Fact]
    public void ResolveConnectionString_WithRelativeBasePath_ResolvesAgainstContentRoot()
    {
        using var contentRoot = new TemporaryDirectory("queue-api-content-");
        var config = ConfigWithConnectionString("TestDb", "Data Source=store.db", "databases");

        var result = Program.ResolveConnectionString(config, contentRoot.FullName, "TestDb");

        new SqliteConnectionStringBuilder(result).DataSource
            .Should().Be(Path.Combine(contentRoot.FullName, "databases", "store.db"));
    }

    /// <summary>
    /// Verifies a relative data source with no base path configured resolves against the content root.
    /// </summary>
    /// <remarks>
    /// Source business rule: spec "When no base directory is configured, relative data sources SHALL
    /// resolve against the application's content root" — the behavior that makes a deployment directory
    /// (with no repository marker) work out of the box.
    /// </remarks>
    [Fact]
    public void ResolveConnectionString_WithRelativeDataSourceAndNoBasePath_ResolvesAgainstContentRoot()
    {
        using var contentRoot = new TemporaryDirectory("queue-api-content-");
        var config = ConfigWithConnectionString("TestDb", "Data Source=db/queue-test.db");

        var result = Program.ResolveConnectionString(config, contentRoot.FullName, "TestDb");

        new SqliteConnectionStringBuilder(result).DataSource
            .Should().Be(Path.Combine(contentRoot.FullName, "db", "queue-test.db"));
    }

    /// <summary>
    /// Verifies the resolved directory is created when it does not exist yet.
    /// </summary>
    /// <remarks>
    /// Source business rule: spec "Database directory is created when missing" — a fresh checkout or
    /// deployment has no <c>db/</c> directory, so startup creates it before the store is opened.
    /// </remarks>
    [Fact]
    public void ResolveConnectionString_WithRelativeDataSource_CreatesMissingDirectory()
    {
        using var tempRoot = new TemporaryDirectory("queue-api-base-");
        var nestedTarget = Path.Combine(tempRoot.FullName, "deep", "nested");
        var config = ConfigWithConnectionString("TestDb", "Data Source=store.db", nestedTarget);

        _ = Program.ResolveConnectionString(config, Path.GetTempPath(), "TestDb");

        Directory.Exists(nestedTarget).Should().BeTrue();
    }

    private static IConfiguration ConfigWithConnectionString(string name, string value, string? basePath = null)
    {
        var values = new Dictionary<string, string?>
        {
            [$"ConnectionStrings:{name}"] = value,
        };
        if (basePath is not null)
        {
            values["Data:DbBasePath"] = basePath;
        }

        return new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
    }
}
