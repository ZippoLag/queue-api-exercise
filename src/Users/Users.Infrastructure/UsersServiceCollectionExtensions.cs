using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Users.Application;

namespace Users.Infrastructure;

/// <summary>
/// Dependency injection registration helpers for the Users API infrastructure.
/// </summary>
public static class UsersServiceCollectionExtensions
{
    /// <summary>
    /// Registers the Users <see cref="UsersDbContext"/> and the read/write entity repositories.
    /// </summary>
    /// <param name="services">The application's service collection.</param>
    /// <param name="connectionString">
    /// The connection string for the shared CMS database holding <c>cms_entities</c>.
    /// </param>
    /// <returns>The same <paramref name="services"/> for chaining.</returns>
    /// <remarks>
    /// The connection string is supplied by the caller from configuration, keeping the library free of
    /// configuration-coupling (same pattern as <c>QueueApi.Auth</c> and <c>CmsWebhook.Infrastructure</c>).
    /// Repositories are scoped so each request gets a short-lived context.
    /// </remarks>
    public static IServiceCollection AddUsersInfrastructure(
        this IServiceCollection services,
        string connectionString)
    {
        services.AddDbContext<UsersDbContext>(options => options.UseSqlite(connectionString));
        services.AddScoped<IEntityQueryRepository, EfEntityQueryRepository>();
        services.AddScoped<IEntityCommandRepository, EfEntityCommandRepository>();

        return services;
    }
}
