using System.ComponentModel.DataAnnotations;

namespace Api.Configuration;

/// <summary>
/// Per-account brute-force lockout (§16). Approved policy: five consecutive failures lock
/// the account for fifteen minutes, and the counter resets on success.
/// </summary>
/// <remarks>
/// <b>This is per account, and it is only half the control.</b> It bounds guessing against
/// one address; it does nothing about one attacker trying one common password against ten
/// thousand addresses, because no single account ever reaches five failures. That shape is
/// §17's per-IP rate limiting. Neither substitutes for the other.
/// <para>
/// Lockout is also a denial-of-service primitive pointed at your own users: anyone who
/// knows an address can keep it locked. That is the accepted trade, and it is why the
/// window is minutes rather than hours and why nothing but a successful login is needed
/// to clear it.
/// </para>
/// </remarks>
public sealed class LockoutOptions
{
    public const string SectionName = "Lockout";

    /// <summary>
    /// Consecutive failures that trigger a lock. Counted on
    /// <see cref="Models.User.FailedLoginCount"/>, reset to zero by a successful login.
    /// </summary>
    /// <remarks>
    /// Lower is not strictly safer. Below about three, ordinary typos lock real users often
    /// enough that support starts unlocking accounts by request — which is a social
    /// engineering path that did not exist before.
    /// </remarks>
    [Range(3, 20)]
    public int MaxFailedAttempts { get; init; } = 5;

    /// <summary>
    /// How long a triggered lock lasts. Measured from the failure that tripped it and
    /// stored in <see cref="Models.User.LockoutEndsAt"/>.
    /// </summary>
    /// <remarks>
    /// The lock expires on its own; nothing sweeps it. A stale
    /// <c>LockoutEndsAt</c> in the past simply reads as "not locked", so no background job
    /// is required and no row is rewritten for a user who never comes back.
    /// </remarks>
    [Range(typeof(TimeSpan), "00:01:00", "24:00:00")]
    public TimeSpan LockoutDuration { get; init; } = TimeSpan.FromMinutes(15);

    /// <summary>
    /// Kill switch for the whole mechanism. Exists for load testing (§26), where five
    /// deliberately-wrong logins otherwise lock the fixture account for the rest of the run.
    /// </summary>
    /// <remarks>
    /// <b>Never set this false outside a test environment.</b> It is deliberately not
    /// environment-scoped in code — a setting whose safety depends on the host silently
    /// disabling itself is worse than one an operator has to type.
    /// </remarks>
    public bool Enabled { get; init; } = true;
}
