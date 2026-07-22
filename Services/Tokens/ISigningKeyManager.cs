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
}
