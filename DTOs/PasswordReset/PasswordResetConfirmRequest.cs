namespace Api.DTOs.PasswordReset;

/// <summary>Body for <c>POST /api/v1/password-reset/confirm</c>.</summary>
/// <remarks>
/// Consuming this bumps <c>User.SecurityStamp</c> and revokes every session
/// (Authentication.md §13) — a password reset exists precisely for the case where someone
/// else may be holding a live session.
/// </remarks>
public sealed record PasswordResetConfirmRequest
{
    /// <summary>Single-use reset token from the email. Hashed at rest.</summary>
    public required string Token { get; init; }

    /// <summary>Plaintext, in transit only. Strength rules are enforced by §10's validator.</summary>
    public required string NewPassword { get; init; }
}
