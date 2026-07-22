namespace Api.DTOs.Auth;

/// <summary>
/// Returned with <c>202 Accepted</c> when the password step succeeded and a second factor
/// is required (Authentication.md §8).
/// </summary>
public sealed record MfaChallengeResponse
{
    /// <summary>
    /// A credential in its own right: single-use, five-minute TTL, stored only as a hash.
    /// It authorises exactly one thing — completing this login — and must never be logged.
    /// </summary>
    public required string MfaTicket { get; init; }

    public required DateTimeOffset ExpiresAt { get; init; }

    /// <summary>
    /// Which factors will be accepted, e.g. <c>totp</c> and <c>recovery</c>. Enumerating
    /// them is safe here: the password step already succeeded, so this discloses nothing to
    /// an attacker who does not already hold the password.
    /// </summary>
    public required IReadOnlyList<string> AcceptedMethods { get; init; }
}
