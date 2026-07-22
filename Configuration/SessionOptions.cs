using System.ComponentModel.DataAnnotations;

namespace Api.Configuration;

/// <summary>
/// Session lifetime bounds. Both must hold for a session to be valid — see
/// <c>Documentation/Architecture/Authentication.md</c> §4 and ADR-0002.
/// </summary>
public sealed class SessionOptions
{
    public const string SectionName = "Session";

    /// <summary>
    /// Sliding inactivity window. A successful refresh slides <c>LastActiveAt</c> forward
    /// by this much. This is what kills an abandoned session on a shared machine in hours
    /// rather than days.
    /// </summary>
    [Range(typeof(TimeSpan), "00:05:00", "24:00:00")]
    public TimeSpan InactivityWindow { get; init; } = TimeSpan.FromHours(6);

    /// <summary>
    /// Hard ceiling measured from login (P1, approved: 7 days). Written once at session
    /// creation and <b>never extended</b> — a refresh slides the inactivity window only.
    /// Extending this on refresh silently defeats the cap.
    /// </summary>
    [Range(typeof(TimeSpan), "01:00:00", "90.00:00:00")]
    public TimeSpan AbsoluteLifetime { get; init; } = TimeSpan.FromDays(7);

    /// <summary>Lifetime of an MFA challenge ticket. Single-use and hashed at rest.</summary>
    public TimeSpan MfaTicketLifetime { get; init; } = TimeSpan.FromMinutes(5);

    /// <summary>Lifetime of a stored WebAuthn challenge.</summary>
    public TimeSpan WebAuthnChallengeLifetime { get; init; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// How often the cleanup worker removes expired sessions and their spent refresh
    /// tokens (§12). Used tokens are retained until the parent session is well past
    /// <see cref="AbsoluteLifetime"/> — deleting them early would break reuse detection,
    /// because a deleted token is indistinguishable from one that never existed.
    /// </summary>
    public TimeSpan CleanupInterval { get; init; } = TimeSpan.FromHours(1);
}
