using CmsWebhook.Application;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using QueueApi.Persistence;

namespace CmsWebhook.Infrastructure;

/// <summary>
/// Dependency injection registration helpers for the CMS webhook infrastructure.
/// </summary>
public static class CmsServiceCollectionExtensions
{
    /// <summary>
    /// Registers the CMS database context, repositories, event processor, outbox channel and worker.
    /// </summary>
    /// <param name="services">The application's service collection.</param>
    /// <param name="connectionString">The connection string for the dedicated CMS database.</param>
    /// <param name="configuration">
    /// The application configuration; <c>Db:Provider</c> selects the EF Core provider (default <c>sqlite</c>).
    /// </param>
    /// <returns>The same <paramref name="services"/> for chaining.</returns>
    /// <remarks>
    /// The connection string is supplied by the caller from configuration, keeping the library free of
    /// configuration-coupling (same pattern as <c>QueueApi.Auth</c>). The EF Core provider is selected via
    /// the shared <see cref="DbContextOptionsBuilderExtensions.UseConfiguredProvider"/> switch, so a future
    /// engine swap is a configuration value, not a source edit (spec "Database provider is configurable").
    /// When <paramref name="configuration"/> is omitted the provider defaults to <c>sqlite</c>, which is what
    /// keeps test plumbing that constructs the registration directly unchanged (design D5 of change
    /// configurable-db-provider). The outbox channel is a singleton shared between the (scoped) ingest
    /// command and the (singleton) worker; repositories and the processor are scoped so each worker sweep
    /// gets a fresh, short-lived context.
    /// </remarks>
    public static IServiceCollection AddCmsWebhookInfrastructure(
        this IServiceCollection services,
        string connectionString,
        IConfiguration? configuration = null)
    {
        var provider = configuration?["Db:Provider"] ?? "sqlite";
        services.AddDbContext<CmsDbContext>(options => options.UseConfiguredProvider(provider, connectionString));

        services.AddScoped<ICmsEventLogRepository, EfCmsEventLogRepository>();
        services.AddScoped<ICmsEntityRepository, EfCmsEntityRepository>();
        services.AddScoped<ICmsEventProcessor, EfCmsEventProcessor>();

        services.AddSingleton<OutboxChannel>();
        services.AddSingleton<IOutboxNotifier>(provider => provider.GetRequiredService<OutboxChannel>());

        services.AddHostedService<CmsEventProcessorWorker>();

        return services;
    }
}
