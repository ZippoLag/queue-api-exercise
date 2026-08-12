namespace QueueApi.Auth;

/// <summary>
/// A user credential stored in the shared database-backed credential store.
/// </summary>
/// <remarks>
/// Spec "Passwords are stored as hashes": the entity holds only the encoded PBKDF2 hash
/// (see <see cref="Pbkdf2PasswordHasher"/>), never the plaintext password. The username length
/// constraint mirrors the architecture's <c>username [10,20]</c> rule (spec "Configured credential format").
/// </remarks>
public class UserCredential
{
    /// <summary>
    /// The store's primary key.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// The unique username, between 10 and 20 characters (architecture: <c>username [10,20]</c>).
    /// </summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// The encoded PBKDF2 password hash produced by <see cref="Pbkdf2PasswordHasher.Hash(string)"/>.
    /// </summary>
    public string PasswordHash { get; set; } = string.Empty;
}
