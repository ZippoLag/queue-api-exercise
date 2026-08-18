using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;

namespace Users.Api.Tests;

/// <summary>
/// Unit tests for the <c>Program</c> startup configuration helpers exposed for testability.
/// </summary>
/// <remarks>
/// Mirrors the CmsWebhook coverage: absolute and in-memory data sources pass through untouched, relative
/// data sources resolve against <c>Data:DbBasePath</c> (falling back to the content root), and the
/// target directory is created when missing.
/// </remarks>
public class UsersApiProgramConfigurationTests
{
    /// <summary>
    /// Verifies a missing connection string fails fast with the config key named in the message.
    /// </summary>
    /// <remarks>
    /// Both stores' connection strings are mandatory startup configuration; a missing key is a setup
    /// error that must fail with guidance, never default silently.
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
    /// Absolute paths are the canonical deployment form; nothing may be prepended to them.
    /// </remarks>
    [Fact]
    public void ResolveConnectionString_WithAbsoluteDataSource_ReturnsUnchanged()
    {
        var connectionString = $"Data Source={Path.Combine(Path.GetTempPath(), "queue-users-absolute.db")}";
        var config = ConfigWithConnectionString("TestDb", connectionString);

        var result = Program.ResolveConnectionString(config, Path.GetTempPath(), "TestDb");

        result.Should().Be(connectionString);
    }

    /// <summary>
    /// Verifies an in-memory data source is returned unchanged.
    /// </summary>
    /// <remarks>
    /// <c>:memory:</c> is a special SQLite data source that must not be resolved against a base directory.
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
    [Fact]
    public void ResolveConnectionString_WithRelativeDataSourceAndConfiguredBasePath_ResolvesAgainstBasePath()
    {
        using var tempRoot = new TemporaryDirectory("queue-api-users-base-");
        var config = ConfigWithConnectionString("TestDb", "Data Source=db/queue-users.db", tempRoot.FullName);

        var result = Program.ResolveConnectionString(config, Path.GetTempPath(), "TestDb");

        new SqliteConnectionStringBuilder(result).DataSource
            .Should().Be(Path.Combine(tempRoot.FullName, "db", "queue-users.db"));
    }

    /// <summary>
    /// Verifies a relative <c>Data:DbBasePath</c> resolves against the content root before joining the data source.
    /// </summary>
    /// <remarks>
    /// The committed Users <c>appsettings.json</c> uses a relative base path pointing at the shared
    /// CmsWebhook directory; this branch keeps such a path meaningful from any content root.
    /// </remarks>
    [Fact]
    public void ResolveConnectionString_WithRelativeBasePath_ResolvesAgainstContentRoot()
    {
        using var contentRoot = new TemporaryDirectory("queue-api-users-content-");
        var config = ConfigWithConnectionString("TestDb", "Data Source=store.db", "../CmsWebhook/CmsWebhook.Api");

        var result = Program.ResolveConnectionString(config, contentRoot.FullName, "TestDb");

        new SqliteConnectionStringBuilder(result).DataSource.Should().Be(Path.GetFullPath(Path.Combine(
            contentRoot.FullName, "..", "CmsWebhook", "CmsWebhook.Api", "store.db")));
    }

    /// <summary>
    /// Verifies a relative data source with no base path configured resolves against the content root.
    /// </summary>
    [Fact]
    public void ResolveConnectionString_WithRelativeDataSourceAndNoBasePath_ResolvesAgainstContentRoot()
    {
        using var contentRoot = new TemporaryDirectory("queue-api-users-content-");
        var config = ConfigWithConnectionString("TestDb", "Data Source=db/queue-users.db");

        var result = Program.ResolveConnectionString(config, contentRoot.FullName, "TestDb");

        new SqliteConnectionStringBuilder(result).DataSource
            .Should().Be(Path.Combine(contentRoot.FullName, "db", "queue-users.db"));
    }

    /// <summary>
    /// Verifies the resolved directory is created when it does not exist yet.
    /// </summary>
    [Fact]
    public void ResolveConnectionString_WithRelativeDataSource_CreatesMissingDirectory()
    {
        using var tempRoot = new TemporaryDirectory("queue-api-users-base-");
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
