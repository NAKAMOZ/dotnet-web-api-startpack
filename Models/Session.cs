using Api.Models.Enums;

namespace Api.Models;

/// <summary>
/// One login on one device. The unit of revocation: everything the API can actually take
/// away mid-flight, it takes away by revoking one of these rows (Authentication.md §13).
/// </summary>
public sealed class Session : IAuditableEntity
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid UserId { get; set; }

    public User User { get; set; } = null!;

    /// <summary>
    /// Client IP as resolved by the forwarded-headers configuration (§16). Recorded so a
    /// user can recognise a session they did not create.
    /// </summary>
    public string? IpAddress { get; set; }

    /// <summary>Raw user agent. Untrusted input — reaches logs as a structured property, never concatenated.</summary>
    public string? UserAgent { get; set; }

    /// <summary>Human-readable label derived from <see cref="UserAgent"/>, for the session list.</summary>
    public string? DeviceLabel { get; set; }

    /// <summary>
    /// When the <b>user</b> last proved an authentication factor. Source of the
    /// <c>auth_time</c> claim and therefore of step-up (Authentication.md §14).
    /// </summary>
    /// <remarks>
    /// Written at login and advanced only by a genuine re-authentication
    /// (<c>ISessionService.MarkReauthenticatedAsync</c>). <b>A refresh must not touch it.</b>
    /// Sliding it on rotation would make every step-up check pass forever on a stolen
    /// session — which is precisely the case the control exists for.
    /// </remarks>
    public DateTimeOffset AuthenticatedAt { get; set; }

    /// <summary>
    /// How this session authenticated. Reissued into the <c>amr</c> claim on every rotation,
    /// which is why it is persisted rather than derived: after a refresh the original login
    /// evidence exists nowhere else.
    /// </summary>
    public ICollection<AuthenticationMethod> AuthenticationMethods { get; set; } = [];

    /// <summary>
    /// Snapshot of <c>User.SecurityStamp</c> as it was at login. Refresh compares the two
    /// and fails with <c>RefreshOutcome.SecurityStampChanged</c> when they diverge.
    /// </summary>
    public required string SecurityStamp { get; set; }

    /// <summary>
    /// Slides forward on every successful refresh. The session is idle-dead once
    /// <c>now &gt; LastActiveAt + AuthSessionOptions.InactivityWindow</c> (6 hours).
    /// </summary>
    public DateTimeOffset LastActiveAt { get; set; }

    /// <summary>
    /// Login time plus the 7-day absolute cap (ADR-0002). <b>Written once, never extended.</b>
    /// </summary>
    /// <remarks>
    /// Refresh slides <see cref="LastActiveAt"/> only. Extending this here — the
    /// helpful-looking change — removes the ceiling entirely and turns a bounded session
    /// into an indefinite one.
    /// </remarks>
    public DateTimeOffset AbsoluteExpiresAt { get; set; }

    /// <summary>Null while live. Set once; a revoked session never returns to active.</summary>
    public DateTimeOffset? RevokedAt { get; set; }

    /// <summary>
    /// Why the session ended. Recorded on every transition into revoked — it is what makes
    /// the audit trail answerable after an incident rather than merely complete.
    /// </summary>
    public SessionRevocationReason? RevocationReason { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>
    /// The rotation chain. Spent tokens are retained, not deleted — reuse detection depends
    /// on telling "already used" apart from "never existed" (Authentication.md §11).
    /// </summary>
    public ICollection<RefreshToken> RefreshTokens { get; } = [];
}
