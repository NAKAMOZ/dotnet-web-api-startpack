namespace Api.DTOs.Mfa;

/// <summary>Body for <c>POST /api/v1/mfa/totp/confirm</c>.</summary>
public sealed record ConfirmTotpRequest
{
    /// <summary>
    /// A code from the freshly enrolled authenticator. Proves the secret transferred
    /// correctly before MFA starts gating logins.
    /// </summary>
    public required string Code { get; init; }
}
