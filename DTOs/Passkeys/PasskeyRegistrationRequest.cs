using System.Text.Json;

namespace Api.DTOs.Passkeys;

/// <summary>Body for <c>POST /api/v1/passkeys/registration/complete</c>.</summary>
public sealed record PasskeyRegistrationRequest
{
    /// <summary>
    /// The authenticator's attestation response, verbatim from
    /// <c>navigator.credentials.create()</c>.
    /// </summary>
    /// <remarks>
    /// Verified against the <b>stored</b> challenge, never against one echoed back in this
    /// payload. A ceremony that trusts the client's copy of the challenge verifies nothing.
    /// </remarks>
    public required JsonElement AttestationResponse { get; init; }

    public string? Label { get; init; }
}
