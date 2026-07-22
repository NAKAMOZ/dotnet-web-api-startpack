using System.Text.Json.Serialization;

namespace Api.DTOs.WellKnown;

/// <summary>
/// The JWKS document at <c>GET /.well-known/jwks.json</c> — unversioned, anonymous, and
/// cacheable.
/// </summary>
/// <remarks>
/// Contains the <c>Active</c> and <c>Retiring</c> keys. Retired keys are omitted, which is
/// what makes retirement mean anything: a token whose <c>kid</c> resolves to nothing is
/// rejected rather than retried against the rest of the ring.
/// </remarks>
public sealed record JwksResponse
{
    [JsonPropertyName("keys")]
    public required IReadOnlyList<JsonWebKeyResponse> Keys { get; init; }
}
