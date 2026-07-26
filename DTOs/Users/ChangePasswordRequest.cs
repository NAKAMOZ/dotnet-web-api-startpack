namespace Api.DTOs.Users;

/// <summary>
/// Body for <c>PUT /api/v1/users/me/password</c>.
/// </summary>
/// <remarks>
/// Requires the current password, which is why this endpoint carries <b>no</b> step-up
/// requirement: proving the current password is stronger evidence than a recent-auth
/// timestamp, and demanding both would mean re-authenticating in order to re-authenticate
/// (Authentication.md §14).
/// <para>
/// Success bumps <c>SecurityStamp</c>, updates the current session's snapshot, and revokes
/// every sibling session.
/// </para>
/// </remarks>
public sealed record ChangePasswordRequest
{
    /// <summary>Plaintext, in transit only. Verified against the stored Argon2id hash.</summary>
    public required string CurrentPassword { get; init; }

    /// <summary>Plaintext, in transit only. Never logged.</summary>
    public required string NewPassword { get; init; }
}
