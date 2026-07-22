namespace Api.Services.Tokens;

/// <summary>
/// Creates, validates and revokes sessions.
/// </summary>
/// <remarks>
/// Implemented in §12. Contract specified in Authentication.md §4 and §13.
/// <para>
/// Implementations take <see cref="TimeProvider"/> rather than calling
/// <c>DateTimeOffset.UtcNow</c> — both lifetime bounds are otherwise untestable without
/// real waiting (ADR-0011).
/// </para>
/// </remarks>
public interface ISessionService
{
    /// <summary>
    /// Creates a session. <c>AbsoluteExpiresAt</c> is set once here, from
    /// <c>SessionOptions.AbsoluteLifetime</c>, and is never modified afterwards.
    /// </summary>
    Task<Guid> CreateAsync(NewSessionRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Slides <c>LastActiveAt</c> after a successful refresh. Must not touch
    /// <c>AbsoluteExpiresAt</c> — extending it would silently defeat the 7-day cap.
    /// </summary>
    Task TouchAsync(Guid sessionId, CancellationToken cancellationToken);

    /// <summary>Revokes one session and every refresh token on it.</summary>
    Task RevokeAsync(Guid sessionId, SessionRevocationReason reason, CancellationToken cancellationToken);

    /// <summary>
    /// Revokes every session for a user, optionally sparing one — pass
    /// <see langword="null"/> for <c>exceptSessionId</c> to revoke all. Used by "revoke all
    /// except current", password change, password reset, and admin revocation.
    /// </summary>
    /// <returns>The number of sessions revoked.</returns>
    Task<int> RevokeAllForUserAsync(
        Guid userId,
        Guid? exceptSessionId,
        SessionRevocationReason reason,
        CancellationToken cancellationToken);
}
