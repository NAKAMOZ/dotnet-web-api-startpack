namespace Api.Models;

/// <summary>
/// A programmatic credential in the form <c>ak_&lt;prefix&gt;_&lt;secret&gt;</c>
/// (Authentication.md §15).
/// </summary>
/// <remarks>
/// API keys are a parallel authentication path, not a session: they create no
/// <see cref="Session"/>, take no part in refresh, and <b>can never satisfy step-up</b>,
/// because no human authenticated and so there is no <c>auth_time</c> to measure.
/// </remarks>
public sealed class ApiKey : IAuditableEntity
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid UserId { get; set; }

    public User User { get; set; } = null!;

    /// <summary>User-supplied name, so a key can be recognised in the list before revoking it.</summary>
    public required string Name { get; set; }

    /// <summary>
    /// The public segment, stored in plaintext and uniquely indexed (§7). It exists so
    /// authentication is one indexed lookup followed by one hash verification, rather than
    /// a hash verification against every key in the table.
    /// </summary>
    public required string KeyPrefix { get; set; }

    /// <summary>
    /// Argon2id hash of the secret segment, using the <b>cheap</b> profile.
    /// </summary>
    /// <remarks>
    /// The fast profile is correct here and wrong for passwords. An API key is
    /// machine-generated high-entropy output with no dictionary to attack, so a work factor
    /// buys nothing but latency on every request. The two profiles are separately named
    /// configuration precisely so this one cannot drift onto the password path — §22
    /// asserts passwords use the slow profile.
    /// </remarks>
    public required string KeyHash { get; set; }

    /// <summary>
    /// Permission constants this key may exercise (Authorization.md §7).
    /// </summary>
    /// <remarks>
    /// The effective set is the <b>intersection</b> of these scopes and the owner's
    /// role-granted permissions, computed at request time. Trusting this column alone would
    /// leave a key acting as an admin after its owner lost the role.
    /// </remarks>
    public ICollection<string> Scopes { get; set; } = [];

    /// <summary>Optional expiry. Null means the key lives until revoked.</summary>
    public DateTimeOffset? ExpiresAt { get; set; }

    /// <summary>Last successful authentication. The signal for spotting a key nobody uses any more.</summary>
    public DateTimeOffset? LastUsedAt { get; set; }

    /// <summary>
    /// Set on revocation. Revoked keys are retained rather than deleted, so an audit row
    /// naming this key still resolves to something.
    /// </summary>
    public DateTimeOffset? RevokedAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
