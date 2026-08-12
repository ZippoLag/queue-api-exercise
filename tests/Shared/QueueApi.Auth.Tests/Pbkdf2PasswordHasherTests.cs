using System.Text.RegularExpressions;
using FluentAssertions;
using QueueApi.Auth;

namespace QueueApi.Auth.Tests;

/// <summary>
/// Unit tests for <see cref="Pbkdf2PasswordHasher"/>.
/// </summary>
public partial class Pbkdf2PasswordHasherTests
{
    private const string SamplePassword = "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa";

    /// <summary>
    /// The self-describing hash format: algorithm tag, iterations, base64 salt, base64 derived key, all joined by <c>$</c>.
    /// </summary>
    [GeneratedRegex("^PBKDF2-SHA256\\$\\d+\\$[A-Za-z0-9+/=]+\\$[A-Za-z0-9+/=]+$")]
    private static partial Regex EncodedHashFormat();

    /// <summary>
    /// Verifies a hash encodes the algorithm tag, the configured iteration count, and base64 salt and derived key of the configured sizes.
    /// </summary>
    /// <remarks>
    /// Source business rule: spec "Passwords are stored as hashes"; the encoded form is self-describing so
    /// the parameters can evolve without breaking stored hashes (design decision D2).
    /// </remarks>
    [Fact]
    public void Hash_ReturnsSelfDescribingEncodedHash()
    {
        var encodedHash = Pbkdf2PasswordHasher.Hash(SamplePassword);

        encodedHash.Should().MatchRegex(EncodedHashFormat());
        var parts = encodedHash.Split('$');
        parts[1].Should().Be(Pbkdf2PasswordHasher.DefaultIterations.ToString());
        Convert.FromBase64String(parts[2]).Should().HaveCount(Pbkdf2PasswordHasher.SaltSize);
        Convert.FromBase64String(parts[3]).Should().HaveCount(Pbkdf2PasswordHasher.DerivedKeySize);
    }

    /// <summary>
    /// Verifies hashing the same password twice produces different salts, hence different hashes.
    /// </summary>
    /// <remarks>
    /// Source business rule: spec "Passwords are stored as hashes"; a per-user random salt prevents
    /// identical passwords from sharing a stored hash.
    /// </remarks>
    [Fact]
    public void Hash_SamePasswordTwice_ProducesDifferentHashes()
    {
        var firstHash = Pbkdf2PasswordHasher.Hash(SamplePassword);
        var secondHash = Pbkdf2PasswordHasher.Hash(SamplePassword);

        firstHash.Should().NotBe(secondHash);
    }

    /// <summary>
    /// Verifies a password verifies successfully against its own hash.
    /// </summary>
    /// <remarks>
    /// Source business rule: spec "Passwords are verified against stored hashes", scenario "Correct password".
    /// </remarks>
    [Fact]
    public void Verify_WithCorrectPassword_ReturnsTrue()
    {
        var encodedHash = Pbkdf2PasswordHasher.Hash(SamplePassword);

        Pbkdf2PasswordHasher.Verify(SamplePassword, encodedHash).Should().BeTrue();
    }

    /// <summary>
    /// Verifies a wrong password fails verification against the stored hash.
    /// </summary>
    /// <remarks>
    /// Source business rule: spec "Passwords are verified against stored hashes", scenario "Incorrect password".
    /// </remarks>
    [Fact]
    public void Verify_WithWrongPassword_ReturnsFalse()
    {
        var encodedHash = Pbkdf2PasswordHasher.Hash(SamplePassword);

        Pbkdf2PasswordHasher.Verify("wrong-password", encodedHash).Should().BeFalse();
    }

    /// <summary>
    /// Verifies malformed encoded hashes fail verification without throwing.
    /// </summary>
    /// <param name="encodedHash">A malformed stored hash.</param>
    /// <remarks>
    /// Source business rule: spec "Passwords are verified against stored hashes"; a corrupt store row must
    /// fail closed as an invalid password rather than crash the request.
    /// </remarks>
    [Theory]
    [InlineData("")]
    [InlineData("garbage")]
    [InlineData("BCRYPT$10$abc$def")]
    [InlineData("PBKDF2-SHA256$not-a-number$AAAA$BBBB")]
    [InlineData("PBKDF2-SHA256$0$AAAA$BBBB")]
    [InlineData("PBKDF2-SHA256$100000$!!!not-base64!!!$AAAA")]
    [InlineData("PBKDF2-SHA256$100000$AAAA")]
    public void Verify_WithMalformedEncodedHash_ReturnsFalse(string encodedHash)
    {
        Pbkdf2PasswordHasher.Verify(SamplePassword, encodedHash).Should().BeFalse();
    }

    /// <summary>
    /// Verifies a null password or encoded hash fails verification instead of throwing.
    /// </summary>
    /// <remarks>
    /// Source business rule: spec "Passwords are verified against stored hashes"; nulls can only come from
    /// programmer error or a corrupt store, and failing closed is the safe outcome.
    /// </remarks>
    [Fact]
    public void Verify_WithNullArguments_ReturnsFalse()
    {
        var encodedHash = Pbkdf2PasswordHasher.Hash(SamplePassword);

        Pbkdf2PasswordHasher.Verify(null!, encodedHash).Should().BeFalse();
        Pbkdf2PasswordHasher.Verify(SamplePassword, null!).Should().BeFalse();
    }

    /// <summary>
    /// Verifies a hash produced with the current settings still verifies after parsing, locking the format over time.
    /// </summary>
    /// <remarks>
    /// Source business rule: spec "Passwords are stored as hashes"; a self-describing format means a hash
    /// written today must remain verifiable after algorithm parameters evolve (design decision D2).
    /// </remarks>
    [Fact]
    public void Verify_HashProducedByCurrentSettings_RoundTrips()
    {
        var encodedHash = Pbkdf2PasswordHasher.Hash(SamplePassword);

        Pbkdf2PasswordHasher.Verify(SamplePassword, encodedHash).Should().BeTrue();
        Pbkdf2PasswordHasher.Hash(SamplePassword).Should().StartWith($"PBKDF2-SHA256${Pbkdf2PasswordHasher.DefaultIterations}$");
    }
}
