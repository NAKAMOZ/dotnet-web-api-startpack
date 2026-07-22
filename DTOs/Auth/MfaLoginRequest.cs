namespace Api.DTOs.Auth;

/// <summary>Second half of an MFA login — <c>POST /api/v1/auth/login/mfa</c>.</summary>
public sealed record MfaLoginRequest
{
    /// <summary>The ticket issued by the password step. Consumed atomically with validation.</summary>
    public required string MfaTicket { get; init; }

    /// <summary>
    /// A TOTP code or a recovery code — the server distinguishes them by shape and records
    /// which was used in <c>amr</c>, because a recovery-code login is a weaker and rarer
    /// event worth seeing in the audit trail.
    /// </summary>
    public required string Code { get; init; }
}
