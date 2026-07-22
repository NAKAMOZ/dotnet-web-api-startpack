namespace Api.Services.Tokens;

/// <summary>
/// A signature and the key that produced it. The <paramref name="KeyId"/> goes into the
/// JWT header as <c>kid</c> so verifiers can resolve the right public key from JWKS
/// without coordination.
/// </summary>
/// <param name="KeyId">The <c>kid</c> of the key that signed.</param>
/// <param name="Signature">Raw ES256 signature bytes (R‖S), ready for base64url encoding.</param>
public sealed record SignatureResult(string KeyId, byte[] Signature);
