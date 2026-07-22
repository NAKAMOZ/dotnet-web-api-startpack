namespace Api.Services.Tokens;

/// <summary>
/// Owns the ES256 key ring: signing, JWKS projection, and rotation.
/// </summary>
/// <remarks>
/// Implemented in §12. Contract specified in Authentication.md §12, ADR-0004, ADR-0020.
/// <para>
/// <b>This is the only component that touches private key material.</b> The interface
/// deliberately exposes <see cref="SignAsync"/> rather than handing out a key: keeping the
/// unprotected key inside one component is what makes the eventual migration from Data
/// Protection to a vault (P7) a change in one place.
/// </para>
/// </remarks>
public interface ISigningKeyManager
{
    /// <summary>Signs a payload with the current <c>Active</c> key.</summary>
    Task<SignatureResult> SignAsync(ReadOnlyMemory<byte> payload, CancellationToken cancellationToken);

    /// <summary>
    /// The <c>kid</c> of the current <c>Active</c> key, generating a key ring if none exists.
    /// </summary>
    /// <remarks>
    /// Added in §12. A JWS signs <c>header.payload</c>, and the header contains the
    /// <c>kid</c> — so the signer's identity has to be known <em>before</em> there is
    /// anything to sign. Without this the issuer would have to sign a throwaway payload
    /// first just to read the key id back off the result.
    /// </remarks>
    Task<string> GetActiveKeyIdAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Public keys to publish: <c>Active</c> and <c>Retiring</c>. Retired keys are omitted.
    /// </summary>
    Task<IReadOnlyList<PublicSigningKey>> GetPublishableKeysAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Generates a new <c>Active</c> key and demotes the current one to <c>Retiring</c>.
    /// </summary>
    /// <remarks>
    /// Does <b>not</b> retire the demoted key — that happens only after
    /// <c>JwtOptions.KeyRetirementGrace</c> has elapsed. Retiring sooner invalidates
    /// tokens still legitimately in flight.
    /// </remarks>
    /// <returns>The <c>kid</c> of the newly active key.</returns>
    Task<string> RotateAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Retires keys whose grace period has elapsed. Called by the maintenance procedure
    /// (§27), not automatically in v1.
    /// </summary>
    Task<int> RetireElapsedKeysAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Resolves a <c>kid</c> to a public key that may validate, or <see langword="null"/>.
    /// </summary>
    /// <remarks>
    /// Added in §12 — the JWT bearer resolver needs it. <b>Exact match against
    /// <c>Active</c> and <c>Retiring</c> only, with no fallback.</b> An implementation that
    /// answers an unresolvable <c>kid</c> by offering the whole ring defeats
    /// <c>kid</c>-based rotation entirely: retired keys would keep validating, so retirement
    /// would stop meaning anything and a leaked old key would stay useful indefinitely.
    /// </remarks>
    Task<System.Security.Cryptography.ECDsa?> ResolveValidationKeyAsync(
        string keyId,
        CancellationToken cancellationToken);
}
