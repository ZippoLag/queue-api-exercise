using FluentAssertions;
using QueueApi.Auth;

namespace QueueApi.Auth.Tests;

/// <summary>
/// Unit tests for the <see cref="UserCredential"/> entity surface.
/// </summary>
public class UserCredentialTests
{
    /// <summary>
    /// Verifies the credential entity retains its assigned values, including the store-assigned primary key.
    /// </summary>
    /// <remarks>
    /// Source business rule: the credential store holds the username and the encoded PBKDF2 hash (spec
    /// &quot;Passwords are stored as hashes&quot;); the primary key is assigned by the store on insert and read
    /// back by EF Core, so both the setter and the getter of <see cref="UserCredential.Id"/> must work.
    /// </remarks>
    [Fact]
    public void UserCredential_WithExplicitValues_RetainsThem()
    {
        var credential = new UserCredential
        {
            Id = 42,
            Username = "cms-webhook",
            PasswordHash = "encoded-hash",
        };

        credential.Id.Should().Be(42);
        credential.Username.Should().Be("cms-webhook");
        credential.PasswordHash.Should().Be("encoded-hash");
    }
}
