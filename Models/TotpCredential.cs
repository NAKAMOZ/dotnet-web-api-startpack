namespace Api.Models;

/// <summary>
/// A user's TOTP authenticator enrolment. At most one per user (unique index on
/// <see cref="UserId"/>, §7).
/// </summary>
public sealed class TotpCredential : IAuditableEntity
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid UserId { get; set; }

    public User User { get; set; } = null!;

    /// <summary>
    /// The shared secret, <b>encrypted</b> — not hashed.
    /// </summary>
    /// <remarks>
    /// The asymmetry with every other secret in this model is deliberate and worth stating:
    /// verifying a TOTP code means recomputing it, which needs the original secret back.
    /// A hash would be one-way and useless here. It is protected at rest by Data Protection
    /// (the same mechanism as signing keys, ADR-0020), never logged, and never returned
    /// after enrolment — the QR payload is shown once and cannot be re-read.
    /// </remarks>
    public required string SecretEncrypted { get; set; }

    /// <summary>
    /// When the user proved the authenticator works by submitting a valid code. Null means
    /// enrolment started but was never completed.
    /// </summary>
    /// <remarks>
    /// An unconfirmed credential must not gate login. Treating enrolment as complete on
    /// creation locks out any user whose authenticator failed to scan — they would face an
    /// MFA challenge they have no way to answer.
    /// </remarks>
    public DateTimeOffset? ConfirmedAt { get; set; }

    /// <summary>The highest TOTP time step successfully accepted for this credential.</summary>
    /// <remarks>
    /// Persisting the matched step makes a code single-use throughout its clock-skew window.
    /// Verification advances it with a conditional database update, so concurrent submissions
    /// of the same code have exactly one winner.
    /// </remarks>
    public long? LastUsedTimeStep { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
