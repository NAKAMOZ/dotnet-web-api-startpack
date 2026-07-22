namespace Api.Services.Crypto;

/// <summary>
/// Argon2id password hashing (ADR-0006).
/// </summary>
/// <remarks>
/// <b>Contract only — <c>Argon2PasswordHasher</c> is §12.</b> The interface lands early
/// because §8's development seeder needs a hash for its fake users, and depending on the
/// contract now means the seeder gains working passwords the moment §12 registers an
/// implementation, with no rework.
/// <para>
/// The parameters live inside the returned hash string, not in configuration. That is what
/// makes <see cref="NeedsRehash"/> possible: raising the configured cost must not
/// invalidate every existing hash at once.
/// </para>
/// </remarks>
public interface IPasswordHasher
{
    /// <summary>Hashes a plaintext password with the current configured parameters.</summary>
    /// <returns>A self-describing hash: algorithm, version, parameters, salt and digest.</returns>
    string Hash(string password);

    /// <summary>
    /// Verifies a password against a stored hash, reading the cost parameters from the hash
    /// itself rather than from current configuration.
    /// </summary>
    /// <remarks>
    /// Must not throw on a malformed stored value — return <see langword="false"/>. A
    /// corrupt hash is an authentication failure, not a 500 that tells the caller their
    /// guess reached something unusual.
    /// </remarks>
    bool Verify(string password, string hash);

    /// <summary>
    /// Whether a stored hash was produced with weaker parameters than the current
    /// configuration, and should be re-hashed.
    /// </summary>
    /// <remarks>
    /// Called after a <em>successful</em> verification — the one moment the plaintext is
    /// available — so the fleet migrates itself as users log in, with no reset emails.
    /// </remarks>
    bool NeedsRehash(string hash);
}
