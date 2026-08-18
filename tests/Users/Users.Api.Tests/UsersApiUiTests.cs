using System.Net;
using FluentAssertions;

namespace Users.Api.Tests;

/// <summary>
/// Integration tests for the hosted browser UI: the application shell and its assets load anonymously,
/// client-side routes fall back to the shell, and the existing endpoints keep their auth semantics.
/// </summary>
public class UsersApiUiTests
{
    /// <summary>
    /// Verifies the origin root serves the browser application shell without credentials.
    /// </summary>
    /// <remarks>
    /// Source business rule: spec "Users API hosts a browser UI", scenario "UI shell loads anonymously" —
    /// the shell must load without credentials so the app can boot and ask the user to sign in.
    /// </remarks>
    [Fact]
    public async Task GetRoot_WithoutCredentials_ReturnsShell()
    {
        using var factory = new UsersApiFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("_framework/blazor.webassembly.js");
    }

    /// <summary>
    /// Verifies a client-side route path falls back to the shell without credentials.
    /// </summary>
    /// <remarks>
    /// Source business rule: spec "Users API hosts a browser UI", scenario
    /// "Client-side routes fall back to the shell" — paths the Blazor router owns must not 401 or 404.
    /// </remarks>
    [Fact]
    public async Task GetClientRoute_WithoutCredentials_ReturnsShell()
    {
        using var factory = new UsersApiFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/some/client/route");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("_framework/blazor.webassembly.js");
    }

    /// <summary>
    /// Verifies protected endpoints keep their auth semantics while the UI is served.
    /// </summary>
    /// <remarks>
    /// Source business rule: spec "Users API hosts a browser UI", scenario "Existing API behavior is
    /// unchanged" — serving the UI must not weaken the fallback policy: the static middleware
    /// short-circuits before authentication (design D2), so the shell is anonymous while every endpoint
    /// stays protected.
    /// </remarks>
    [Fact]
    public async Task GetEntities_WithoutCredentials_StillReturnsUnauthorized()
    {
        using var factory = new UsersApiFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/entities");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
