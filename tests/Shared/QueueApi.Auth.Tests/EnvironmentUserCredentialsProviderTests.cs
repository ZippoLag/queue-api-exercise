using FluentAssertions;
using QueueApi.Auth;

namespace QueueApi.Auth.Tests;

/// <summary>
/// Unit tests for <see cref="EnvironmentUserCredentialsProvider"/>.
/// </summary>
public class EnvironmentUserCredentialsProviderTests
{
    private const string ValidUsername = "cms-webhook";
    private const string ValidPassword = "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa";

    /// <summary>
    /// A username with 9 characters, below the architecture's <c>[10,20]</c> minimum.
    /// </summary>
    private const string TooShortUsername = "123456789";

    /// <summary>
    /// A username with 21 characters, above the architecture's <c>[10,20]</c> maximum.
    /// </summary>
    private const string TooLongUsername = "123456789012345678901";

    /// <summary>
    /// A username with exactly 10 characters, the architecture's minimum allowed length.
    /// </summary>
    private const string MinimumLengthUsername = "1234567890";

    /// <summary>
    /// A username with exactly 20 characters, the architecture's maximum allowed length.
    /// </summary>
    private const string MaximumLengthUsername = "12345678901234567890";

    /// <summary>
    /// Verifies the provider resolves the configured username to its configured password.
    /// </summary>
    /// <remarks>
    /// Source business rule: spec "Credentials are sourced from environment variables", scenario
    /// "Environment variables are set".
    /// </remarks>
    [Fact]
    public void GetPassword_ForConfiguredUsername_ReturnsConfiguredPassword()
    {
        var provider = CreateProvider(ValidUsername, ValidPassword);

        var password = provider.GetPassword(ValidUsername);

        password.Should().Be(ValidPassword);
    }

    /// <summary>
    /// Verifies the provider returns <see langword="null"/> for a username that is not configured.
    /// </summary>
    /// <remarks>
    /// Source business rule: spec "All endpoints require authentication", scenario
    /// "Request with credentials of an unknown user".
    /// </remarks>
    [Fact]
    public void GetPassword_ForUnknownUsername_ReturnsNull()
    {
        var provider = CreateProvider(ValidUsername, ValidPassword);

        var password = provider.GetPassword("unknown-user");

        password.Should().BeNull();
    }

    /// <summary>
    /// Verifies the configured username is exposed for use by the authorization policy.
    /// </summary>
    /// <remarks>
    /// Source business rule: spec "Only the cms user is authorized"; the policy needs the configured
    /// username to tell the cms user apart from any other authenticated user.
    /// </remarks>
    [Fact]
    public void Username_ReturnsConfiguredUsername()
    {
        var provider = CreateProvider(ValidUsername, ValidPassword);

        provider.Username.Should().Be(ValidUsername);
    }

    /// <summary>
    /// Verifies construction fails with a descriptive error when the username variable is not set.
    /// </summary>
    /// <remarks>
    /// Source business rule: spec "Credentials are sourced from environment variables", scenario
    /// "Environment variables are missing".
    /// </remarks>
    [Fact]
    public void Ctor_WhenUsernameVariableIsMissing_Throws()
    {
        var act = () => CreateProvider(username: null, ValidPassword);

        act.Should().Throw<InvalidOperationException>().WithMessage("*AUTH_CMS_USERNAME*");
    }

    /// <summary>
    /// Verifies construction fails with a descriptive error when the password variable is not set.
    /// </summary>
    /// <remarks>
    /// Source business rule: spec "Credentials are sourced from environment variables", scenario
    /// "Environment variables are missing".
    /// </remarks>
    [Fact]
    public void Ctor_WhenPasswordVariableIsMissing_Throws()
    {
        var act = () => CreateProvider(ValidUsername, password: null);

        act.Should().Throw<InvalidOperationException>().WithMessage("*AUTH_CMS_PASSWORD*");
    }

    /// <summary>
    /// Verifies construction fails when the configured username is shorter than 10 or longer than 20 characters.
    /// </summary>
    /// <param name="username">The username to configure.</param>
    /// <remarks>
    /// Source business rule: spec "Configured credential format", scenario "Invalid configured username
    /// length"; architecture: <c>username [10,20]</c> characters.
    /// </remarks>
    [Theory]
    [InlineData(TooShortUsername)]
    [InlineData(TooLongUsername)]
    public void Ctor_WhenUsernameLengthIsOutsideAllowedRange_Throws(string username)
    {
        var act = () => CreateProvider(username, ValidPassword);

        act.Should().Throw<InvalidOperationException>().WithMessage("*between 10 and 20*");
    }

    /// <summary>
    /// Verifies construction succeeds for usernames exactly at the 10 and 20 character boundaries.
    /// </summary>
    /// <param name="username">The username to configure.</param>
    /// <remarks>
    /// Source business rule: spec "Configured credential format", scenario "Valid configured username
    /// length"; architecture: <c>username [10,20]</c> characters.
    /// </remarks>
    [Theory]
    [InlineData(MinimumLengthUsername)]
    [InlineData(MaximumLengthUsername)]
    public void Ctor_WhenUsernameLengthIsAtAllowedBoundaries_DoesNotThrow(string username)
    {
        var act = () => CreateProvider(username, ValidPassword);

        act.Should().NotThrow();
    }

    private static EnvironmentUserCredentialsProvider CreateProvider(string? username, string? password)
    {
        var variables = new Dictionary<string, string?>
        {
            [EnvironmentUserCredentialsProvider.UsernameEnvironmentVariable] = username,
            [EnvironmentUserCredentialsProvider.PasswordEnvironmentVariable] = password,
        };
        return new EnvironmentUserCredentialsProvider(
            name => variables.TryGetValue(name, out var value) ? value : null);
    }
}
