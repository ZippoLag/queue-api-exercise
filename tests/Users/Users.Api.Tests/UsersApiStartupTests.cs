using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using QueueApi.Auth;

namespace Users.Api.Tests;

/// <summary>
/// Tests for the Users API startup fail-fast behavior: the credential store must hold the administrator
/// user, and configured reserved usernames must satisfy the <c>[10,20]</c> length rule.
/// </summary>
public class UsersApiStartupTests
{
    /// <summary>
    /// Verifies the host fails to start when the store has not been initialized with the schema.
    /// </summary>
    /// <remarks>
    /// Source business rule: design decision 4 of the users-api-vertical change — fail fast with setup
    /// guidance mirroring the CmsWebhook check; the provider wraps the missing-table failure.
    /// </remarks>
    [Fact]
    public void CreateClient_WhenStoreIsNotInitialized_ThrowsWithGuidance()
    {
        var emptyDatabasePath = Path.GetTempFileName();
        try
        {
            using var factory = new UsersApiFactory(authDbConnectionString: $"Data Source={emptyDatabasePath}");

            var exception = CaptureStartupFailure(factory);

            exception.Message.Should().Contain("scripts/init-db.sh");
        }
        finally
        {
            File.Delete(emptyDatabasePath);
        }
    }

    /// <summary>
    /// Verifies the host fails to start when the store is initialized but lacks the administrator user.
    /// </summary>
    /// <remarks>
    /// Source business rule: design decision 4 — without the administrator user the admin endpoints and
    /// the administrator's listing view are unreachable, so startup must fail with guidance.
    /// </remarks>
    [Fact]
    public void CreateClient_WhenStoreLacksAdministratorUser_ThrowsWithGuidance()
    {
        var databasePath = CreateInitializedStoreWithoutUsers();
        try
        {
            using var factory = new UsersApiFactory(authDbConnectionString: $"Data Source={databasePath}");

            var exception = CaptureStartupFailure(factory);

            exception.Message.Should().Contain("does not contain the administrator user");
            exception.Message.Should().Contain("scripts/init-db.sh");
        }
        finally
        {
            File.Delete(databasePath);
        }
    }

    /// <summary>
    /// Verifies the host fails to start when the configured administrator username violates the
    /// <c>[10,20]</c> length rule.
    /// </summary>
    /// <remarks>
    /// Source business rule: architecture <c>username [10,20]</c>, enforced for the reserved usernames
    /// (mirroring the CmsWebhook's <c>Auth:CmsUsername</c> check). The invalid username is injected via
    /// the <c>Auth__AdministratorUsername</c> environment variable, which is part of the default
    /// configuration chain read during the host build.
    /// </remarks>
    [Fact]
    public void CreateClient_WhenAdministratorUsernameLengthIsInvalid_ThrowsAtStartup()
    {
        var previousValue = Environment.GetEnvironmentVariable("Auth__AdministratorUsername");
        Environment.SetEnvironmentVariable("Auth__AdministratorUsername", "123456789");
        try
        {
            using var factory = new UsersApiFactory();

            var exception = CaptureStartupFailure(factory);

            exception.Message.Should().Contain("between 10 and 20");
        }
        finally
        {
            Environment.SetEnvironmentVariable("Auth__AdministratorUsername", previousValue);
        }
    }

    /// <summary>
    /// Verifies the host fails to start when the configured cms username violates the length rule.
    /// </summary>
    /// <remarks>
    /// Source business rule: the cms rejection name is also a reserved username and gets the same
    /// <c>[10,20]</c> validation (architecture rule, shared <c>ResolveUsername</c> helper).
    /// </remarks>
    [Fact]
    public void CreateClient_WhenCmsUsernameLengthIsInvalid_ThrowsAtStartup()
    {
        var previousValue = Environment.GetEnvironmentVariable("Auth__CmsUsername");
        Environment.SetEnvironmentVariable("Auth__CmsUsername", "123456789");
        try
        {
            using var factory = new UsersApiFactory();

            var exception = CaptureStartupFailure(factory);

            exception.Message.Should().Contain("between 10 and 20");
        }
        finally
        {
            Environment.SetEnvironmentVariable("Auth__CmsUsername", previousValue);
        }
    }

    private static InvalidOperationException CaptureStartupFailure(UsersApiFactory factory)
    {
        var exception = Record.Exception(() => factory.CreateClient());
        var invalidOperation = exception as InvalidOperationException
            ?? exception?.InnerException as InvalidOperationException;
        invalidOperation.Should().NotBeNull($"expected an InvalidOperationException, but got '{exception}'");
        return invalidOperation!;
    }

    private static string CreateInitializedStoreWithoutUsers()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"queue-api-users-auth-empty-{Guid.NewGuid():N}.db");
        var options = new DbContextOptionsBuilder<AuthDbContext>()
            .UseSqlite($"Data Source={databasePath}")
            .Options;
        using var context = new AuthDbContext(options);
        context.Database.EnsureCreated();
        return databasePath;
    }
}
