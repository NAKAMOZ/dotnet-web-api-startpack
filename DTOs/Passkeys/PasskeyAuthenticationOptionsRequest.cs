namespace Api.DTOs.Passkeys;

/// <summary>
/// Body for the anonymous <c>POST /api/v1/passkeys/authentication/options</c>.
/// </summary>
public sealed record PasskeyAuthenticationOptionsRequest
{
    /// <summary>
    /// Optional hint for non-discoverable credentials.
    /// </summary>
    /// <remarks>
    /// The response must be identical whether or not the address exists — a shorter
    /// credential list, a different error, or a faster reply all turn this anonymous
    /// endpoint into an account-enumeration oracle. Omitting it entirely is the better
    /// path: discoverable credentials need no hint.
    /// </remarks>
    public string? Email { get; init; }
}
