namespace Api.DTOs.Passkeys;

/// <summary>Body for <c>POST /api/v1/passkeys/registration/options</c> — an authenticated user.</summary>
public sealed record PasskeyRegistrationOptionsRequest
{
    /// <summary>
    /// Optional name for the credential ("YubiKey", "iPhone"), carried through the ceremony
    /// so the list is readable afterwards.
    /// </summary>
    public string? Label { get; init; }
}
