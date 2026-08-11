using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace QueueApi.Auth;

/// <summary>
/// Dependency injection registration helpers for the Basic authentication scheme.
/// </summary>
public static class BasicAuthenticationServiceCollectionExtensions
{
    /// <summary>
    /// Registers the Basic authentication scheme, its environment-backed credential provider and options validation.
    /// </summary>
    /// <param name="services">The application's service collection.</param>
    /// <returns>The same <paramref name="services"/> for chaining.</returns>
    /// <remarks>
    /// The scheme is registered as the default authenticate and challenge scheme so that
    /// <c>ChallengeAsync</c> (issued by the authorization middleware for <c>401</c>) routes to the Basic handler.
    /// Options validation runs at startup (design decision 4: fail fast on misconfiguration); the credential
    /// provider itself validates the environment variables when it is first resolved.
    /// </remarks>
    public static IServiceCollection AddBasicAuthentication(this IServiceCollection services)
    {
        services.AddSingleton<IUserCredentialsProvider, EnvironmentUserCredentialsProvider>();

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
