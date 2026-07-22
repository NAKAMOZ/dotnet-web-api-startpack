namespace Api.Models;

/// <summary>
/// One link in a session's rotation chain. Single-use: presenting one mints its successor
/// and burns it (Authentication.md §6).
/// </summary>
public sealed class RefreshToken : IAuditableEntity
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    /// <summary>
    /// The session this token belongs to. Rotation validates <b>this</b> session, not one
    /// named by the caller — which is what makes a cross-session replay impossible rather
    /// than merely unlikely.
    /// </summary>
    public Guid SessionId { get; set; }

    public Session Session { get; set; } = null!;

    /// <summary>
    /// SHA-256 of the opaque token, base64url-encoded. <b>The plaintext is never stored</b>
    /// — it exists once, in the response that issued it (ADR-0001).
    /// </summary>
    /// <remarks>
    /// A plain hash rather than a slow one is correct here: the token is 256 bits of CSPRNG
    /// output, so there is no dictionary to attack and nothing for a work factor to buy.
    /// Unique index (§7) — a hash collision must be a database error, not a silent
    /// cross-session handover.
    /// </remarks>
    public required string TokenHash { get; set; }

    /// <summary>Own expiry, always bounded by the owning session's absolute cap.</summary>
    public DateTimeOffset ExpiresAt { get; set; }

    /// <summary>
    /// When this token was spent. Null means unused; non-null on a second presentation is
    /// the reuse signal, and reuse revokes the entire session (Authentication.md §7).
    /// </summary>
    public DateTimeOffset? UsedAt { get; set; }

    /// <summary>
    /// The successor issued when this token was spent. Reconstructs the chain during an
    /// incident review — how far back the replay reached, and which rotation forked it.
    /// </summary>
    public Guid? ReplacedByTokenId { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
