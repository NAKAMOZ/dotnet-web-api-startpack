namespace Api.Services.Tokens;

/// <summary>
/// Result of a rotation attempt. Tokens are present only when
/// <see cref="Outcome"/> is <see cref="RefreshOutcome.Rotated"/>.
/// </summary>
public sealed record RefreshResult
{
    public required RefreshOutcome Outcome { get; init; }

    /// <summary>Non-null only on success.</summary>
    public IssuedAccessToken? AccessToken { get; init; }

    /// <summary>Non-null only on success.</summary>
    public IssuedRefreshToken? RefreshToken { get; init; }

    /// <summary>Session the presented token belonged to, when one was found. For auditing.</summary>
    public Guid? SessionId { get; init; }

    public static RefreshResult Success(
        Guid sessionId,
        IssuedAccessToken accessToken,
        IssuedRefreshToken refreshToken) =>
        new()
        {
            Outcome = RefreshOutcome.Rotated,
            SessionId = sessionId,
            AccessToken = accessToken,
            RefreshToken = refreshToken,
        };

    public static RefreshResult Failure(RefreshOutcome outcome, Guid? sessionId = null) =>
        new() { Outcome = outcome, SessionId = sessionId };
}
