namespace Api.Exceptions;

/// <summary>
/// An already-spent refresh token was presented again.
/// </summary>
/// <remarks>
/// Either an attacker is replaying a stolen token or a client retried. The two are
/// indistinguishable, so the safe reading is assumed: <b>the entire session is revoked</b>,
/// not just the token, and <c>token_reuse_detected</c> is audited (Authentication.md §7).
/// <para>
/// The legitimate user is logged out as a result. That is the intended trade — an attacker
/// silently coexisting on a live session is a far worse outcome than a re-login.
/// </para>
/// </remarks>
public sealed class TokenReuseDetectedException(Guid sessionId)
    : DomainException("token_reuse_detected", "The session has been revoked.")
{
    /// <summary>The session that was revoked. For the audit record.</summary>
    public Guid SessionId { get; } = sessionId;
}
