using System.Text.Json;

namespace Api.DTOs.Passkeys;

/// <summary>
/// WebAuthn creation options, passed straight to <c>navigator.credentials.create()</c>.
/// </summary>
/// <remarks>
/// Modelled as raw JSON rather than as typed records. Two reasons: the WebAuthn option set
/// is large and versioned by the spec, not by us, and re-declaring it here would put a
/// second, drifting copy of Fido2NetLib's model in the public contract. §12 produces this
/// from the library; the library's types never appear in an API signature.
/// <para>
/// The challenge inside is stored server-side, single-use, with a five-minute TTL. It is
/// not something the client may choose.
/// </para>
/// </remarks>
public sealed record PasskeyRegistrationOptionsResponse
{
    public required JsonElement Options { get; init; }

    public required DateTimeOffset ExpiresAt { get; init; }
}
