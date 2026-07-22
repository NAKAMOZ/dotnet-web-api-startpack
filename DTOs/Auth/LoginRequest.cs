namespace Api.DTOs.Auth;

/// <summary>Credentials for <c>POST /api/v1/auth/login</c>.</summary>
public sealed record LoginRequest
{
    public required string Email { get; init; }

    /// <summary>Plaintext, in transit only. Never logged, never echoed back in an error.</summary>
    public required string Password { get; init; }
}
