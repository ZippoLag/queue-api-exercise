using Microsoft.AspNetCore.Authorization;
using QueueApi.Auth;
using System.Security.Claims;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddBasicAuthentication();

var cmsUsername = Environment.GetEnvironmentVariable(EnvironmentUserCredentialsProvider.UsernameEnvironmentVariable)
    ?? throw new InvalidOperationException(
        $"Missing required environment variable '{EnvironmentUserCredentialsProvider.UsernameEnvironmentVariable}'. "
        + "Basic authentication credentials for the CMS API must be provided via environment variables.");

builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .RequireClaim(ClaimTypes.Name, cmsUsername)
        .Build();
});

var app = builder.Build();

// Fail fast: resolving the provider validates the environment at startup instead of surfacing as runtime 401s.
_ = app.Services.GetRequiredService<IUserCredentialsProvider>();

app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/", () => "Hello World!");

app.Run();

/// <summary>
/// Exposes the web application entry point to integration tests via <c>WebApplicationFactory&lt;Program&gt;</c>.
/// </summary>
public partial class Program
{
}
