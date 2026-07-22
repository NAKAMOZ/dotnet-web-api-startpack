namespace Api.Models.Enums;

/// <summary>
/// Recorded on every transition into the revoked state. Without it the audit trail can say
/// a session ended but not why, which is the question actually asked after an incident
/// (Authentication.md §11).
/// </summary>
public enum SessionRevocationReason
{
    Logout,
    UserRevokedSession,
    UserRevokedAllSessions,
    PasswordChanged,
    PasswordReset,

    /// <summary>A used refresh token was presented again. The whole session dies, loudly.</summary>
    TokenReuseDetected,

    AdminRevoked,
    AccountDeleted,
    MfaDisabled,
}
