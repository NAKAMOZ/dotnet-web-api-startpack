using System.Text.Json.Serialization;

namespace Api.DTOs.WellKnown;

/// <summary>
/// One JWK in the published key set. <b>Public components only</b> — every field here is
/// served anonymously at <c>/.well-known/jwks.json</c>.
/// </summary>
/// <remarks>
/// Property names are the RFC 7517 short forms, so they are set explicitly rather than left
/// to the serializer's casing policy: <c>kty</c> is not what camelCase makes of
/// <c>KeyType</c>, and a verifier that cannot find the field simply fails.
/// <para>
/// Publishing this is safe <em>only while <c>alg</c> stays pinned to ES256</em>. A validator
/// that reads the algorithm from the token header would let an attacker sign with HS256
/// using this very public key as the HMAC secret (Authentication.md §2).
/// </para>
/// </remarks>
public sealed record JsonWebKeyResponse
{
    [JsonPropertyName("kty")]
    public required string KeyType { get; init; }

    [JsonPropertyName("use")]
    public required string Use { get; init; }

    [JsonPropertyName("alg")]
    public required string Algorithm { get; init; }

    /// <summary>The key id token headers carry. Resolution is exact-match, with no fallback.</summary>
    [JsonPropertyName("kid")]
    public required string KeyId { get; init; }

    [JsonPropertyName("crv")]
    public required string Curve { get; init; }

    /// <summary>Base64url x coordinate.</summary>
    [JsonPropertyName("x")]
    public required string X { get; init; }

    /// <summary>Base64url y coordinate.</summary>
    [JsonPropertyName("y")]
    public required string Y { get; init; }
}
