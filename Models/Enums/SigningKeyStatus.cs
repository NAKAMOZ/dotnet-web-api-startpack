namespace Api.Models.Enums;

/// <summary>
/// Position of a key in the ES256 ring (Authentication.md §12, ADR-0004).
/// </summary>
/// <remarks>
/// The three states exist because signing and validating must be able to disagree. A key
/// that stops signing immediately would invalidate every token it already signed, so
/// demotion and retirement are separate steps separated by at least
/// <c>JwtOptions.KeyRetirementGrace</c> (access TTL + clock skew).
/// </remarks>
public enum SigningKeyStatus
{
    /// <summary>Signs new tokens, validates, published in JWKS. Exactly one key at a time.</summary>
    Active,

    /// <summary>
    /// No longer signs; still validates and is still published, because tokens it signed
    /// are legitimately in flight.
    /// </summary>
    Retiring,

    /// <summary>
    /// Dead. Does not validate and is not published. A token whose <c>kid</c> resolves to a
    /// retired key is rejected — there is no fallback to another key, which is the whole
    /// point of <c>kid</c>-based rotation (Authentication.md §12).
    /// </summary>
    Retired,
}
