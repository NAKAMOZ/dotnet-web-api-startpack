using System.Text.Json;

namespace Api.DTOs.Passkeys;

/// <summary>
/// WebAuthn request options, passed to <c>navigator.credentials.get()</c>. Raw JSON for the
/// same reason as the registration options.
/// </summary>
public sealed record PasskeyAuthenticationOptionsResponse
{
    public required JsonElement Options { get; init; }

    public required DateTimeOffset ExpiresAt { get; init; }
}
