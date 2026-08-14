using System.Net;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;

namespace CmsWebhook.Api.Tests;

/// <summary>
/// Integration tests for starting the API from a deployment-like directory that contains no repository
/// marker file.
/// </summary>
/// <remarks>
/// Source business rule: spec "No repository marker is required" and "Database directory is created when
/// missing" — published deployments ship only the application and its <c>appsettings.json</c>; relative
/// data sources must resolve against the content root and the target directory must be created.
/// </remarks>
public class CmsWebhookApiDeploymentStartupTests
{
    /// <summary>
    /// Verifies the API starts from a directory with no <c>QueueApi.slnx</c> marker and creates the
    /// resolved database directory.
    /// </summary>
    /// <remarks>
    /// The deployment directory mirrors a publish output: only <c>appsettings.json</c> with relative data
    /// sources, no solution marker anywhere above it. The factory swaps the DbContexts to throwaway
    /// stores, but the startup fail-fast still opens the resolved CMS path (WAL pragma), which proves the
    /// relative data source resolved against the deployment directory and created its <c>db/</c> folder.
    /// </remarks>
    [Fact]
    public async Task Startup_FromDeploymentDirectoryWithoutRepositoryMarker_SucceedsAndCreatesDatabaseDirectory()
    {
        using var deploymentDirectory = new TemporaryDirectory("queue-api-deploy-");
        var sourceAppSettings = Path.Combine(
            FindProjectDirectory(), "appsettings.json");
        File.Copy(sourceAppSettings, Path.Combine(deploymentDirectory.FullName, "appsettings.json"));
        File.Exists(Path.Combine(deploymentDirectory.FullName, "QueueApi.slnx")).Should().BeFalse();

        using var factory = new CmsWebhookApiFactory();
        using var client = factory
            .WithWebHostBuilder(builder => builder.UseContentRoot(deploymentDirectory.FullName))
            .CreateClient();

        var response = await client.GetAsync("/health");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        File.Exists(Path.Combine(deploymentDirectory.FullName, "db", "queue-cms.db")).Should().BeTrue();
    }

    private static string FindProjectDirectory()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "QueueApi.slnx")))
            {
                return Path.Combine(directory.FullName, "src", "CmsWebhook", "CmsWebhook.Api");
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Test must run inside the repository to locate CmsWebhook.Api.csproj.");
    }
}
