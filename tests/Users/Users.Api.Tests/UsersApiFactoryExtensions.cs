using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using QueueApi.Auth;

namespace Users.Api.Tests;

/// <summary>
/// Extension helpers for <see cref="UsersApiFactory"/>.
/// </summary>
public static class UsersApiFactoryExtensions
{
    /// <summary>
    /// Returns a host that verifies credentials against a fixed in-memory set instead of the store.
    /// </summary>
    /// <param name="factory">The base factory sharing its temporary stores.</param>
    /// <param name="users">The known users as <c>(Username, Password)</c> tuples.</param>
    /// <returns>A derived host with the credential provider swapped.</returns>
    /// <remarks>
    /// The derived host reuses the base factory's temporary stores (the base
    /// <c>ConfigureWebHost</c> overrides still apply), so tests keep seeding entities through the
    /// original factory while authenticating users that are not in the store.
    /// </remarks>
    public static WebApplicationFactory<Program> WithCredentialProvider(
        this UsersApiFactory factory,
        params (string Username, string Password)[] users)
        => factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.RemoveAll<IUserCredentialsProvider>();
            services.AddScoped<IUserCredentialsProvider>(_ => new InMemoryUserCredentialsProvider(users));
        }));
}
