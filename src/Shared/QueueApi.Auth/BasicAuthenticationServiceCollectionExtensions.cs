using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using QueueApi.Persistence;

namespace QueueApi.Auth;

/// <summary>
/// Dependency injection registration helpers for the Basic authentication scheme.
/// </summary>
public static class BasicAuthenticationServiceCollectionExtensions
{
    /// <summary>
    /// Registers the Basic authentication scheme, the database-backed credential provider and options validation.
    /// </summary>
    /// <param name="services">The application's service collection.</param>
    /// <param name="connectionString">The connection string for the shared credential store.</param>
    /// <param name="configuration">
    /// The application configuration; <c>Db:Provider</c> selects the EF Core provider (default <c>sqlite</c>).
    /// </param>
    /// <returns>The same <paramref name="services"/> for chaining.</returns>
    /// <remarks>
    /// Spec "Credential store location is configurable": the connection string is supplied by the caller from
    /// configuration, keeping the shared library free of configuration-coupling. The EF Core provider is
    /// selected via the shared <see cref="DbContextOptionsBuilderExtensions.UseConfiguredProvider"/> switch, so
    /// a future engine swap is a configuration value, not a source edit (spec "Database provider is
    /// configurable"); omitting <paramref name="configuration"/> defaults the provider to <c>sqlite</c> (design
    /// D5 of change configurable-db-provider). The scheme is registered as the default authenticate and
    /// challenge scheme so that <c>ChallengeAsync</c> (issued by the authorization middleware for <c>401</c>)
    /// routes to the Basic handler. Options validation runs at startup (design decision 4 of the original
    /// change: fail fast on misconfiguration).
    /// </remarks>
    public static IServiceCollection AddBasicAuthentication(
        this IServiceCollection services,
        string connectionString,
        IConfiguration? configuration = null)
    {
        var provider = configuration?["Db:Provider"] ?? "sqlite";
        services.AddDbContext<AuthDbContext>(options => options.UseConfiguredProvider(provider, connectionString));
        services.AddScoped<IUserCredentialsProvider, DbUserCredentialsProvider>();

        services.AddOptions<BasicAuthenticationOptions>()
            .Validate(options => !string.IsNullOrWhiteSpace(options.Realm),
                $"{nameof(BasicAuthenticationOptions.Realm)} must not be empty.")
            .ValidateOnStart();

        services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = BasicAuthenticationDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = BasicAuthenticationDefaults.AuthenticationScheme;
                options.DefaultForbidScheme = BasicAuthenticationDefaults.AuthenticationScheme;
            })
            .AddScheme<BasicAuthenticationOptions, BasicAuthenticationHandler>(
                BasicAuthenticationDefaults.AuthenticationScheme,
                configureOptions: null);

        return services;
    }
}
