namespace Api.Services.Tokens;

/// <summary>
/// One entry in the published JWKS document. **Public components only** — this type must
/// never gain a field carrying private key material, because everything in it is served
/// anonymously at <c>/.well-known/jwks.json</c> (§22 asserts this).
/// </summary>
/// <param name="KeyId">The <c>kid</c>.</param>
/// <param name="Curve">JWK <c>crv</c>. Always <c>P-256</c> for ES256.</param>
/// <param name="X">Base64url-encoded x coordinate.</param>
/// <param name="Y">Base64url-encoded y coordinate.</param>
public sealed record PublicSigningKey(string KeyId, string Curve, string X, string Y)
{
    /// <summary>JWK <c>kty</c>.</summary>
    public string KeyType => "EC";

    /// <summary>JWK <c>use</c>.</summary>
    public string Use => "sig";

    /// <summary>JWK <c>alg</c>.</summary>
    public string Algorithm => "ES256";
}
