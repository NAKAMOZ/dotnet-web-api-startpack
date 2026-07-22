using System.Text.Json;

namespace Api.DTOs.Passkeys;

/// <summary>
/// Body for <c>POST /api/v1/passkeys/authentication/complete</c>. On success this creates a
/// session with <c>amr: [webauthn]</c>.
/// </summary>
public sealed record PasskeyAuthenticationRequest
{
    /// <summary>
    /// The assertion, verbatim from <c>navigator.credentials.get()</c>. Its signature is
    /// verified against the stored public key, and its signature counter against the stored
    /// one — a counter that fails to advance means two devices answer for one credential.
    /// </summary>
    public required JsonElement AssertionResponse { get; init; }
}
