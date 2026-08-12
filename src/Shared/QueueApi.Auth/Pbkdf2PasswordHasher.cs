using System.Security.Cryptography;
using System.Text;

namespace QueueApi.Auth;

/// <summary>
/// Hashes and verifies passwords using PBKDF2-HMAC-SHA256 with a per-user random salt.
/// </summary>
/// <remarks>
/// Spec "Passwords are stored as hashes" and "Passwords are verified against stored hashes":
/// the credential store never holds plaintext passwords, and verification re-derives the hash with
/// the stored salt and compares it in constant time. The encoded hash is self-describing
/// (<c>PBKDF2-SHA256$&lt;iterations&gt;$&lt;base64 salt&gt;$&lt;base64 derived key&gt;</c>) so the
/// iteration count can be raised in the future without breaking previously stored hashes
/// (design decision D2). 100,000 iterations is the OWASP-recommended floor for PBKDF2-HMAC-SHA256.
/// </remarks>
public static class Pbkdf2PasswordHasher
{
    /// <summary>
    /// The algorithm tag embedded in every encoded hash, identifying the KDF and hash function.
    /// </summary>
    public const string AlgorithmTag = "PBKDF2-SHA256";

    /// <summary>
    /// The number of PBKDF2 iterations used when hashing a new password.
    /// </summary>
    public const int DefaultIterations = 100_000;

    /// <summary>
    /// The size in bytes of the random salt generated per password.
    /// </summary>
    public const int SaltSize = 16;

    /// <summary>
    /// The size in bytes of the derived key; matches the SHA-256 output size.
    /// </summary>
    public const int DerivedKeySize = 32;

    /// <summary>
    /// Hashes <paramref name="password"/> with a fresh random salt and returns the self-describing encoded hash.
    /// </summary>
    /// <param name="password">The plaintext password to hash.</param>
    /// <returns>The encoded hash in the format <c>PBKDF2-SHA256$&lt;iterations&gt;$&lt;base64 salt&gt;$&lt;base64 derived key&gt;</c>.</returns>
    /// <exception cref="ArgumentNullException">When <paramref name="password"/> is <see langword="null"/>.</exception>
    public static string Hash(string password)
    {
        ArgumentNullException.ThrowIfNull(password);

        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var derivedKey = Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(password), salt, DefaultIterations, HashAlgorithmName.SHA256, DerivedKeySize);

        return string.Join('$', AlgorithmTag, DefaultIterations, Convert.ToBase64String(salt), Convert.ToBase64String(derivedKey));
    }

    /// <summary>
    /// Verifies <paramref name="password"/> against an encoded hash by re-deriving the key with the stored salt.
    /// </summary>
    /// <param name="password">The plaintext password to verify.</param>
    /// <param name="encodedHash">The encoded hash produced by <see cref="Hash(string)"/>.</param>
    /// <returns>
    /// <see langword="true"/> when the re-derived key matches the stored one (compared in constant time),
    /// <see langword="false"/> for a wrong password or a malformed encoded hash.
    /// </returns>
    public static bool Verify(string password, string encodedHash)
    {
        if (password is null || encodedHash is null)
        {
            return false;
        }

        if (!TryParse(encodedHash, out var iterations, out var salt, out var expectedKey))
        {
            return false;
        }

        // The iteration count is read from the trusted store row; the self-describing format makes this
        // possible, and the store is assumed trustworthy (a tampered store can already reject logins).
        var derivedKey = Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(password), salt, iterations, HashAlgorithmName.SHA256, expectedKey.Length);

        return CryptographicOperations.FixedTimeEquals(derivedKey, expectedKey);
    }

    private static bool TryParse(string encodedHash, out int iterations, out byte[] salt, out byte[] derivedKey)
    {
        iterations = 0;
        salt = Array.Empty<byte>();
        derivedKey = Array.Empty<byte>();

        var parts = encodedHash.Split('$');
        if (parts.Length != 4
            || !string.Equals(parts[0], AlgorithmTag, StringComparison.Ordinal)
            || !int.TryParse(parts[1], out iterations)
            || iterations <= 0)
        {
            return false;
        }

        try
        {
            salt = Convert.FromBase64String(parts[2]);
            derivedKey = Convert.FromBase64String(parts[3]);
        }
        catch (FormatException)
        {
            return false;
        }

        return salt.Length > 0 && derivedKey.Length > 0;
    }
}
