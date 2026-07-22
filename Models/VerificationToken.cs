using Api.Models.Enums;

namespace Api.Models;

/// <summary>
/// A short-lived, single-use, hashed-at-rest credential. One table serves email
/// verification, password reset, MFA tickets and WebAuthn challenges — they differ in
/// lifetime and effect, not in shape.
/// </summary>
public sealed class VerificationToken : IAuditableEntity
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    /// <summary>
    /// Owner, or null for a <see cref="VerificationTokenType.PasskeyAuthenticationChallenge"/>
    /// — that ceremony begins before any user is identified.
    /// </summary>
    /// <remarks>
    /// Nullable for exactly that one type. Every other type must set it, and §7 does not
    /// relax the foreign key: a token with no owner can authorise nothing but the ceremony
    /// that produced it.
    /// </remarks>
    public Guid? UserId { get; set; }

    public User? User { get; set; }

    /// <summary>
    /// What this token authorises. <b>Part of the lookup, not a label</b> — a reset token
    /// presented to the verification endpoint must fail to resolve, not resolve and then
    /// get type-checked.
    /// </summary>
    public required VerificationTokenType Type { get; set; }

    /// <summary>
    /// SHA-256 of the opaque value, base64url-encoded. Unique index (§7). The plaintext
    /// leaves the process once, in the email or response that carries it, and is never
    /// logged.
    /// </summary>
    public required string TokenHash { get; set; }

    /// <summary>
    /// Expiry. Minutes for MFA tickets and WebAuthn challenges, hours for email links —
    /// all from <c>AuthSessionOptions</c>, none hard-coded at the call site.
    /// </summary>
    public DateTimeOffset ExpiresAt { get; set; }

    /// <summary>
    /// When the token was spent. Set atomically with the validation that accepted it — a
    /// check-then-consume gap lets two concurrent requests both pass and complete the same
    /// one-shot operation twice.
    /// </summary>
    public DateTimeOffset? ConsumedAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
