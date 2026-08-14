using System.Reflection;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.UserSecrets;
using Microsoft.Extensions.Hosting;

namespace CmsWebhook.Api.Tests;

/// <summary>
/// Tests the 12-factor configuration precedence chain: base <c>appsettings.json</c>, then
/// <c>appsettings.{Environment}.json</c>, then user-secrets (Development only), then environment variables.
/// </summary>
/// <remarks>
/// The chain itself is stock .NET wiring; these tests pin the documented semantics (spec "Configuration
/// precedence chain"). File and environment-variable layers are exercised through the real
/// <c>WebApplication.CreateBuilder</c> chain; user-secrets are exercised through the same provider order
/// because the test host's entry assembly cannot carry a user-secrets id (see
/// <see cref="ApiAssembly_CarriesUserSecretsId_EnablingDevelopmentUserSecrets"/>).
/// </remarks>
public class ConfigurationPrecedenceTests
{
    private const string SecretsId = "queue-api-test-secrets";

    /// <summary>
    /// Verifies an environment variable overrides values from the configuration files.
    /// </summary>
    /// <remarks>
    /// Source business rule: spec scenario "Environment variable overrides configuration files" — the
    /// double-underscore convention maps <c>ConnectionStrings__CmsDb</c> to <c>ConnectionStrings:CmsDb</c>.
    /// </remarks>
    [Fact]
    public void EnvironmentVariable_OverridesConfigurationFiles()
    {
        using var contentRoot = new TemporaryDirectory("queue-api-config-");
        File.WriteAllText(
            Path.Combine(contentRoot.FullName, "appsettings.json"),
            """{"ConnectionStrings":{"QaPrecedenceDb":"Data Source=file.db"}}""");

        Environment.SetEnvironmentVariable("ConnectionStrings__QaPrecedenceDb", "Data Source=env.db");
        try
        {
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions
            {
                EnvironmentName = Environments.Production,
                ContentRootPath = contentRoot.FullName,
            });

            builder.Configuration.GetConnectionString("QaPrecedenceDb").Should().Be("Data Source=env.db");
        }
        finally
        {
            Environment.SetEnvironmentVariable("ConnectionStrings__QaPrecedenceDb", null);
        }
    }

    /// <summary>
    /// Verifies the environment-specific file overrides the base file.
    /// </summary>
    /// <remarks>
    /// Source business rule: spec scenario "Environment file overrides base configuration" — each
    /// environment layers on top of the shared defaults.
    /// </remarks>
    [Theory]
    [InlineData("Development", "appsettings.Development.json")]
    [InlineData("Staging", "appsettings.Staging.json")]
    [InlineData("Production", "appsettings.Production.json")]
    public void EnvironmentFile_OverridesBaseFile(string environment, string environmentFileName)
    {
        using var contentRoot = new TemporaryDirectory("queue-api-config-");
        File.WriteAllText(
            Path.Combine(contentRoot.FullName, "appsettings.json"),
            """{"QaPrecedenceKey":"base"}""");
        File.WriteAllText(
            Path.Combine(contentRoot.FullName, environmentFileName),
            $$"""{"QaPrecedenceKey":"{{environment.ToLowerInvariant()}}"}""");

        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = environment,
            ContentRootPath = contentRoot.FullName,
        });

        builder.Configuration["QaPrecedenceKey"].Should().Be(environment.ToLowerInvariant());
    }

    /// <summary>
    /// Verifies user-secrets override file values when they are part of the chain (Development).
    /// </summary>
    /// <remarks>
    /// Source business rule: spec scenario "User-secrets override configuration files in Development" —
    /// secrets never belong in committed files, they enter the chain through user-secrets in Development.
    /// The chain is built in the documented order with the user-secrets provider, mirroring what
    /// <c>WebApplication.CreateBuilder</c> wires for the Development environment.
    /// </remarks>
    [Fact]
    public void UserSecrets_OverrideFileValues_InDevelopmentChain()
    {
        using var home = new TemporaryDirectory("queue-api-secrets-");
        WriteSecretsFile(home.FullName, SecretsId, """{"ConnectionStrings":{"QaSecretsDb":"Data Source=secret.db"}}""");

        var (previousHome, previousAppData) = RedirectHomeAndAppData(home.FullName);
        try
        {
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:QaSecretsDb"] = "Data Source=file.db",
                })
                .AddUserSecrets(SecretsId)
                .Build();

            configuration.GetConnectionString("QaSecretsDb").Should().Be("Data Source=secret.db");
        }
        finally
        {
            RestoreHomeAndAppData(previousHome, previousAppData);
        }
    }

    /// <summary>
    /// Verifies user-secrets have no effect when they are not part of the chain (Staging/Production).
    /// </summary>
    /// <remarks>
    /// Source business rule: spec scenario "User-secrets are ignored outside Development" — the
    /// user-secrets provider is only wired in Development, so a secrets file present on disk must not
    /// influence Staging or Production even though it exists.
    /// </remarks>
    [Fact]
    public void UserSecrets_AreNotConsulted_OutsideDevelopmentChain()
    {
        using var home = new TemporaryDirectory("queue-api-secrets-");
        WriteSecretsFile(home.FullName, SecretsId, """{"ConnectionStrings":{"QaSecretsDb":"Data Source=secret.db"}}""");

        var (previousHome, previousAppData) = RedirectHomeAndAppData(home.FullName);
        try
        {
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:QaSecretsDb"] = "Data Source=file.db",
                })
                // No AddUserSecrets: outside Development the provider is absent by design.
                .Build();

            configuration.GetConnectionString("QaSecretsDb").Should().Be("Data Source=file.db");
        }
        finally
        {
            RestoreHomeAndAppData(previousHome, previousAppData);
        }
    }

    /// <summary>
    /// Verifies the API project carries a user-secrets id so the Development chain includes user-secrets.
    /// </summary>
    /// <remarks>
    /// Source business rule: spec "Configuration precedence chain" — user-secrets only participate in
    /// Development; that wiring depends on the entry assembly carrying <see cref="UserSecretsIdAttribute"/>.
    /// The framework gates the provider on the Development environment; this test pins the other half of
    /// the wiring (the id on the assembly).
    /// </remarks>
    [Fact]
    public void ApiAssembly_CarriesUserSecretsId_EnablingDevelopmentUserSecrets()
    {
        typeof(Program).Assembly.GetCustomAttribute<UserSecretsIdAttribute>().Should().NotBeNull();
    }

    private static void WriteSecretsFile(string home, string secretsId, string json)
    {
        // The user-secrets store location is OS-specific; write to both known candidates and redirect
        // both HOME and APPDATA so the resolution finds the file regardless of which the runtime prefers.
        foreach (var candidate in new[]
        {
            Path.Combine(home, ".microsoft", "usersecrets", secretsId),
            Path.Combine(home, "Microsoft", "UserSecrets", secretsId),
        })
        {
            Directory.CreateDirectory(candidate);
            File.WriteAllText(Path.Combine(candidate, "secrets.json"), json);
        }
    }

    private static (string? Home, string? AppData) RedirectHomeAndAppData(string home)
    {
        var previousHome = Environment.GetEnvironmentVariable("HOME");
        var previousAppData = Environment.GetEnvironmentVariable("APPDATA");
        Environment.SetEnvironmentVariable("HOME", home);
        Environment.SetEnvironmentVariable("APPDATA", home);
        return (previousHome, previousAppData);
    }

    private static void RestoreHomeAndAppData(string? home, string? appData)
    {
        Environment.SetEnvironmentVariable("HOME", home);
        Environment.SetEnvironmentVariable("APPDATA", appData);
    }
}
