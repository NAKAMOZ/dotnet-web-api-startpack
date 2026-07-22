namespace Api.DTOs.Auth;

/// <summary>Registration payload for <c>POST /api/v1/auth/register</c>.</summary>
public sealed record RegisterRequest
{
    public required string Email { get; init; }

    /// <summary>
    /// Plaintext, in transit only. Argon2id-hashed before it reaches the database and
    /// never logged — the destructuring policy (§15) drops this property by name.
    /// </summary>
    public required string Password { get; init; }

    public string? DisplayName { get; init; }
}
