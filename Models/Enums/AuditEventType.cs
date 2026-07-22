namespace Api.Models.Enums;

/// <summary>
/// The security audit catalog. Every member here is a security-relevant event that must
/// survive log rotation and stay queryable — which is why the audit trail is a table and
/// not a Serilog sink (§15).
/// </summary>
/// <remarks>
/// This enum is the closed set: §15 wires an <c>IAuditLogger</c> call for each member and a
/// guard test asserts the catalog in <c>Documentation/Architecture/AuditTrail.md</c> matches
/// it. Adding an event means adding a member here first.
/// <para>
/// Persisted as a string (§7). The documentation writes the same events in snake_case
/// (<c>login_failed</c>); the stored form is the member name.
/// </para>
/// </remarks>
public enum AuditEventType
{
    /// <summary>A new account was created — by registration or by first social login.</summary>
    UserRegistered,

    LoginSucceeded,

    /// <summary>
    /// Recorded for unknown user, wrong password and locked account alike. The audit row
    /// may distinguish them in <c>Metadata</c>; the HTTP response must not
    /// (Authentication.md §5).
    /// </summary>
    LoginFailed,

    MfaChallengeIssued,
    MfaFailed,

    /// <summary>Five consecutive failures. Invisible to the client, visible here.</summary>
    AccountLocked,

    TokenRefreshed,

    /// <summary>
    /// A used refresh token was presented again. The loudest event in the catalog: it means
    /// either a stolen token or a client bug, and the whole session was revoked for it
    /// (Authentication.md §7).
    /// </summary>
    TokenReuseDetected,

    /// <summary>Any transition into revoked. The <c>SessionRevocationReason</c> goes in <c>Metadata</c>.</summary>
    SessionRevoked,

    PasswordChanged,
    PasswordResetRequested,
    PasswordResetCompleted,
    EmailVerified,
    MfaEnrolled,

    /// <summary>Step-up protected (Authentication.md §14) — an attacker on a live session would start here.</summary>
    MfaDisabled,

    PasskeyRegistered,
    PasskeyRemoved,
    ApiKeyCreated,
    ApiKeyRevoked,
    RoleGranted,
    RoleRevoked,
    AdminUserDeleted,

    /// <summary>Key ring rotated. Never records key material — only the new <c>kid</c>.</summary>
    SigningKeyRotated,
}
