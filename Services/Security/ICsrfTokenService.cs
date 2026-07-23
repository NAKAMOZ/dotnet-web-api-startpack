namespace Api.Services.Security;

/// <summary>
/// Mints and verifies the session-bound CSRF token used by cookie mode
/// (Authentication.md §3).
/// </summary>
/// <remarks>
/// One component owns both halves so they cannot disagree about the format. §14's filter is
/// the verifier; §12's <c>GET /api/v1/auth/csrf</c> is the only issuer.
/// </remarks>
public interface ICsrfTokenService
{
    /// <summary>Issues a token bound to <paramref name="sessionId"/>.</summary>
    /// <remarks>
    /// The same value goes into the readable <c>__Host-auth.csrf</c> cookie and into the
    /// response body; the client copies it into <c>X-CSRF-Token</c> on every state-changing
    /// request. Both halves are checked — the double submit proves the caller can read the
    /// cookie, and the binding proves the token was minted for this session.
    /// </remarks>
    string Issue(Guid sessionId);

    /// <summary>
    /// Whether <paramref name="token"/> was issued by this API for
    /// <paramref name="sessionId"/> and has not expired.
    /// </summary>
    /// <remarks>
    /// Returns <see langword="false"/> for every failure — malformed, forged, expired, or
    /// minted for another session. The caller gets one answer with no detail, deliberately:
    /// distinguishing "expired" from "wrong session" tells an attacker which half of a
    /// forgery attempt worked.
    /// </remarks>
    bool Validate(string? token, Guid sessionId);
}
