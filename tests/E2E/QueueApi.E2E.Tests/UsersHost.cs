extern alias users;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using QueueApi.Auth;
using Users.Infrastructure;

namespace QueueApi.E2E.Tests;

/// <summary>
/// Test host for the Users API wired to the shared end-to-end stores.
/// </summary>
/// <remarks>
/// The host is a <see cref="WebApplicationFactory{TEntryPoint}"/> over the Users API's entry point,
/// with the auth and Users contexts pointed at the shared temporary stores the whole scenario runs
/// against (same pattern as <c>UsersApiFactory</c>, recreated here because the E2E project references
/// both APIs and therefore aliases their <c>Program</c> types).
/// </remarks>
public sealed class UsersHost : WebApplicationFactory<users::Program>
{
    private readonly string _authDbConnectionString;
    private readonly string _cmsDbConnectionString;

    /// <summary>
    /// Creates the host over the given shared stores.
    /// </summary>
    /// <param name="authDbConnectionString">The connection string of the shared credential store.</param>
    /// <param name="cmsDbConnectionString">The connection string of the shared CMS database.</param>
    public UsersHost(string authDbConnectionString, string cmsDbConnectionString)
    {
        _authDbConnectionString = authDbConnectionString;
        _cmsDbConnectionString = cmsDbConnectionString;
    }

    /// <inheritdoc/>
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // Replaces the DbContexts registered by the app so the startup fail-fast check (administrator
            // present) and every request talk to the shared stores.
            services.RemoveAll<AuthDbContext>();
            services.RemoveAll<DbContextOptions<AuthDbContext>>();
            services.AddDbContext<AuthDbContext>(options => options.UseSqlite(_authDbConnectionString));

            services.RemoveAll<UsersDbContext>();
            services.RemoveAll<DbContextOptions<UsersDbContext>>();
            services.AddDbContext<UsersDbContext>(options => options.UseSqlite(_cmsDbConnectionString));
        });
    }
}
