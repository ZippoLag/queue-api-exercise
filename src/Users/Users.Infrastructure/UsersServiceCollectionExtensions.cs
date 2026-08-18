using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using QueueApi.Persistence;
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
    /// <param name="configuration">
    /// The application configuration; <c>Db:Provider</c> selects the EF Core provider (default <c>sqlite</c>).
    /// </param>
    /// <returns>The same <paramref name="services"/> for chaining.</returns>
    /// <remarks>
    /// The connection string is supplied by the caller from configuration, keeping the library free of
    /// configuration-coupling (same pattern as <c>QueueApi.Auth</c> and <c>CmsWebhook.Infrastructure</c>).
    /// The EF Core provider is selected via the shared
    /// <see cref="DbContextOptionsBuilderExtensions.UseConfiguredProvider"/> switch, so a future engine swap
    /// is a configuration value, not a source edit (spec "Database provider is configurable"). When
    /// <paramref name="configuration"/> is omitted the provider defaults to <c>sqlite</c> (design D5 of
    /// change configurable-db-provider). Repositories are scoped so each request gets a short-lived context.
    /// </remarks>
    public static IServiceCollection AddUsersInfrastructure(
        this IServiceCollection services,
        string connectionString,
        IConfiguration? configuration = null)
    {
        var provider = configuration?["Db:Provider"] ?? "sqlite";
        services.AddDbContext<UsersDbContext>(options => options.UseConfiguredProvider(provider, connectionString));
        services.AddScoped<IEntityQueryRepository, EfEntityQueryRepository>();
        services.AddScoped<IEntityCommandRepository, EfEntityCommandRepository>();

        return services;
    }
}
