namespace Api.DTOs.Sessions;

/// <summary>
/// Result of a bulk revocation — <c>DELETE /api/v1/sessions</c> and the admin equivalent.
/// </summary>
/// <remarks>
/// Returns a count rather than 204 because the number is the confirmation: a user revoking
/// every other session wants to know how many there were, and an unexpectedly high number
/// is exactly the signal that should prompt a password change.
/// </remarks>
public sealed record RevokeSessionsResponse
{
    public required int RevokedCount { get; init; }
}
