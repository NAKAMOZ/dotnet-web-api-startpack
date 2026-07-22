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
    /// <c>AuthSessionOptions.AbsoluteLifetime</c>, and is never modified afterwards.
    /// </summary>
    Task<Guid> CreateAsync(NewSessionRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Slides <c>LastActiveAt</c> after a successful refresh. Must not touch
    /// <c>AbsoluteExpiresAt</c> — extending it would silently defeat the 7-day cap.
    /// </summary>
    Task TouchAsync(Guid sessionId, CancellationToken cancellationToken);

    /// <summary>
    /// Advances the session's recorded authentication time after a genuine
    /// re-authentication, so subsequent tokens carry a fresh <c>auth_time</c> and
    /// step-up-protected operations become available (Authentication.md §14).
    /// </summary>
    /// <remarks>
    /// Called <b>only</b> from a completed authentication flow. Calling it from
    /// <see cref="TouchAsync"/> or from the refresh path would make every rotation look
    /// like a re-authentication and defeat step-up entirely.
    /// </remarks>
    Task MarkReauthenticatedAsync(Guid sessionId, CancellationToken cancellationToken);

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
