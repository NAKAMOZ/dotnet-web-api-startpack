namespace Api.DTOs.Auth;

/// <summary>
/// Result of a successful registration.
/// </summary>
/// <remarks>
/// Registration does <b>not</b> return tokens. The account exists but has not proven its
/// email address, and issuing a session here would make the verification step optional in
/// practice. The client logs in as a separate, explicit step.
/// </remarks>
public sealed record RegisterResponse
{
    public required Guid UserId { get; init; }

    public required string Email { get; init; }

    /// <summary>Always <see langword="false"/> here — a verification email has been sent.</summary>
    public required bool EmailVerified { get; init; }
}
