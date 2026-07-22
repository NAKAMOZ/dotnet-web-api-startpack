namespace Api.Services.Tokens;

/// <summary>
/// Why a refresh succeeded or failed. The distinctions matter: they drive different audit
/// events and let the API tell a client "you were idle" apart from "your session aged out"
/// (ADR-0002).
/// </summary>
public enum RefreshOutcome
{
    /// <summary>Rotated successfully. New access and refresh tokens issued.</summary>
    Rotated,

    /// <summary>No token matches the presented hash.</summary>
    NotFound,

    /// <summary>
    /// The token was already used. Treated as theft, not as a retry: the entire session is
    /// revoked and <c>token_reuse_detected</c> is audited (Authentication.md §7).
    /// </summary>
    ReuseDetected,

    /// <summary>Past the token's own expiry.</summary>
    TokenExpired,

    /// <summary>Session idle beyond the sliding inactivity window.</summary>
    SessionIdle,

    /// <summary>Session past its absolute cap. No amount of activity would have helped.</summary>
    SessionExpired,

    /// <summary>Session was revoked — logout, password change, admin action, or earlier reuse.</summary>
    SessionRevoked,

    /// <summary>
    /// <c>User.SecurityStamp</c> changed since the session was created. The global per-user
    /// kill switch, checked here rather than per request to keep access-token validation
    /// stateless.
    /// </summary>
    SecurityStampChanged,
}
