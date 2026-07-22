using Api.Models.Enums;

namespace Api.Models;

/// <summary>
/// One ES256 key in the signing ring (Authentication.md §12, ADR-0004, ADR-0020). The only
/// entity with no owning user — keys belong to the deployment.
/// </summary>
public sealed class SigningKey : IAuditableEntity
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    /// <summary>
    /// The <c>kid</c> published in JWKS and written into every token header. Unique (§7).
    /// Resolution is exact-match: an unknown or retired <c>kid</c> is rejected, never
    /// retried against another key.
    /// </summary>
    public required string KeyId { get; set; }

    /// <summary>
    /// The private key, protected by ASP.NET Core Data Protection (ADR-0020).
    /// </summary>
    /// <remarks>
    /// <b>Never logged, never serialised into a response, never included in a Problem
    /// Details payload.</b> Only <c>ISigningKeyManager</c> unprotects it, and that interface
    /// exposes <c>SignAsync</c> rather than handing the key out — which is what keeps the
    /// eventual move to a vault (P7) a change in one component.
    /// </remarks>
    public required string PrivateKeyProtected { get; set; }

    /// <summary>
    /// The public key, base64-encoded SubjectPublicKeyInfo. Projected into JWKS <c>x</c>
    /// and <c>y</c> coordinates on read. Public by design — publishing it is safe only
    /// while <c>alg</c> stays pinned to ES256 (Authentication.md §2).
    /// </summary>
    public required string PublicKey { get; set; }

    public required SigningKeyStatus Status { get; set; }

    /// <summary>When the key became <see cref="SigningKeyStatus.Active"/>.</summary>
    public DateTimeOffset ActivatedAt { get; set; }

    /// <summary>
    /// When the key was demoted to <see cref="SigningKeyStatus.Retiring"/>. The grace period
    /// is measured from here.
    /// </summary>
    /// <remarks>
    /// Retirement is only legal once <c>JwtOptions.KeyRetirementGrace</c> — access TTL plus
    /// clock skew — has elapsed since this moment. Without the column the elapsed time has
    /// no anchor and "wait long enough" becomes a matter of operator memory.
    /// </remarks>
    public DateTimeOffset? RetiringAt { get; set; }

    /// <summary>When the key stopped validating. Retired rows are kept as a rotation record.</summary>
    public DateTimeOffset? RetiredAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
