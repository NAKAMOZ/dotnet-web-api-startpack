namespace Api.DTOs.EmailVerification;

/// <summary>Confirmation that an address is now verified.</summary>
public sealed record EmailVerifiedResponse
{
    public required string Email { get; init; }

    public required DateTimeOffset VerifiedAt { get; init; }
}
