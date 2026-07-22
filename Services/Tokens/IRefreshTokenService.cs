namespace Api.Services.Tokens;

/// <summary>
/// Issues, rotates and revokes opaque refresh tokens, and detects replay.
/// </summary>
/// <remarks>
/// Implemented in §12. Contract specified in Authentication.md §6–§7.
/// <para>
/// Rotation must be atomic: marking the presented token used, linking the successor, and
/// sliding the session must succeed or fail together. A partial rotation either burns a
/// token without issuing a replacement, or leaves two live tokens on one chain — which
/// makes reuse detection unreliable.
/// </para>
/// </remarks>
public interface IRefreshTokenService
{
    /// <summary>Issues the first refresh token for a newly created session.</summary>
    Task<IssuedRefreshToken> IssueAsync(Guid sessionId, CancellationToken cancellationToken);

    /// <summary>
    /// Validates the presented plaintext token — as read from the cookie or request body —
    /// and on success marks it used and issues a successor pair.
    /// </summary>
    /// <remarks>
    /// Never throws for an invalid token. An invalid presentation is an expected outcome,
    /// not an exceptional one, and is reported through <see cref="RefreshResult.Outcome"/>.
    /// </remarks>
    Task<RefreshResult> RotateAsync(string presentedToken, CancellationToken cancellationToken);

    /// <summary>Invalidates every outstanding token on a session. Called whenever a session is revoked.</summary>
    Task RevokeForSessionAsync(Guid sessionId, CancellationToken cancellationToken);
}
