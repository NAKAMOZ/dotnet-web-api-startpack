using Api.Configuration;
using Api.Models;
using Microsoft.Extensions.Options;

namespace Api.Services.Security;

/// <summary>
/// The per-account lockout state machine (§16). Pure: it reads the clock and mutates the
/// <see cref="User"/> it is handed, and touches nothing else.
/// </summary>
/// <remarks>
/// <b>Recorded deviation from §16.</b> The roadmap puts this logic inside
/// <c>Services/Auth/LoginService</c>. It lives in its own type because the login path also
/// does credential verification, MFA branching, session creation and token issuance — and
/// §22 has to assert lockout boundaries exactly, which through the login service means a
/// database, a password hash and a full request per case. The service still owns the
/// decision to call this; only the arithmetic moved.
/// <para>
/// <b>Nothing here is allowed to reach the caller.</b> Whether an account is locked is not
/// a fact the client may learn: a locked account answers with the same
/// <c>invalid_credentials</c> code and body as a wrong password, and takes the same time
/// to do it — see <c>Documentation/Security/Enumeration.md</c>. This type returns a bool
/// to the login service, never a reason to a response.
/// </para>
/// </remarks>
public sealed class LockoutPolicy(IOptions<LockoutOptions> options, TimeProvider timeProvider)
{
    private readonly LockoutOptions _options = options.Value;

    /// <summary>
    /// Whether <paramref name="user"/> is locked right now.
    /// </summary>
    /// <remarks>
    /// A lock in the past reads as "not locked" without being cleared, so an expired lockout
    /// needs no sweep job and no write for a user who never returns.
    /// </remarks>
    public bool IsLockedOut(User user) =>
        _options.Enabled
        && user.LockoutEndsAt is { } endsAt
        && endsAt > timeProvider.GetUtcNow();

    /// <summary>
    /// Records a failed authentication attempt and applies a lock once the threshold is
    /// reached. Mutates <paramref name="user"/>; the caller persists.
    /// </summary>
    /// <returns><see langword="true"/> if this failure is the one that locked the account.</returns>
    public bool RegisterFailure(User user)
    {
        if (!_options.Enabled)
        {
            return false;
        }

        // TODO §16: implement the failure transition — see the notes below.
        //
        // State available to you:
        //   user.FailedLoginCount   int, consecutive failures so far
        //   user.LockoutEndsAt      DateTimeOffset?, null when not locked
        //   _options.MaxFailedAttempts   5
        //   _options.LockoutDuration     15 minutes
        //   timeProvider.GetUtcNow()     never DateTimeOffset.UtcNow
        //
        // Decisions this body has to make, none of which have a single obvious answer:
        //
        //   1. A lock has expired but FailedLoginCount is still at the threshold from last
        //      time. Does this failure re-lock immediately, or does the user get a fresh
        //      allowance? Approved policy is a fresh allowance — so a stale counter has to
        //      be cleared somewhere, and this is the only method that runs on that path.
        //   2. On locking, does FailedLoginCount reset to zero or stay at the threshold?
        //      Whichever you pick, (1) must still hold afterwards.
        //   3. Return true only on the transition into lockout, not on every failure while
        //      locked — the caller writes the `account_locked` audit event from this, and a
        //      duplicate row per attempt makes the audit trail useless for exactly the
        //      incident it exists to describe.
        throw new NotImplementedException("§16: lockout failure transition");
    }

    /// <summary>
    /// Records a successful authentication. Clears the counter and any expired lock, so a
    /// user who mistypes four times and then succeeds is not one mistake from lockout
    /// tomorrow (Authentication.md §5).
    /// </summary>
    public void RegisterSuccess(User user)
    {
        user.FailedLoginCount = 0;
        user.LockoutEndsAt = null;
    }
}
