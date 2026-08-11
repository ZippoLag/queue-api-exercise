using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using QueueApi.Auth;

namespace CmsWebhook.Api.Tests;

/// <summary>
/// Test host for the CMS Webhook API that can swap the credential provider to exercise the 403 path.
/// </summary>
/// <remarks>
/// Spec "Only the cms user is authorized": production config only ever contains the cms user, so the
/// non-cms authorized user is injected here via the provider seam (design decision 7).
/// </remarks>
public class CmsWebhookApiFactory : WebApplicationFactory<Program>
{
    private readonly IUserCredentialsProvider? _credentialsProviderOverride;

    /// <summary>
    /// Creates the factory, optionally replacing the environment-backed credential provider.
    /// </summary>
    /// <param name="credentialsProviderOverride">The provider to use instead of <see cref="EnvironmentUserCredentialsProvider"/>, or <see langword="null"/>.</param>
    public CmsWebhookApiFactory(IUserCredentialsProvider? credentialsProviderOverride = null)
    {
        _credentialsProviderOverride = credentialsProviderOverride;
    }

    /// <inheritdoc/>
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        if (_credentialsProviderOverride is not null)
        {
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IUserCredentialsProvider>();
                services.AddSingleton<IUserCredentialsProvider>(_credentialsProviderOverride);
            });
        }
    }
}
