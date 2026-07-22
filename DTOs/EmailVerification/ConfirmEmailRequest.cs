namespace Api.DTOs.EmailVerification;

/// <summary>Body for the anonymous <c>POST /api/v1/email-verification/confirm</c>.</summary>
public sealed record ConfirmEmailRequest
{
    /// <summary>
    /// The token from the verification email. Single-use, hashed at rest, and looked up by
    /// hash together with its type — a password-reset token presented here must not resolve.
    /// </summary>
    public required string Token { get; init; }
}
