using Api.Configuration;
using Api.Logging;
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
public sealed class LockoutPolicy(
    IOptions<LockoutOptions> options,
    TimeProvider timeProvider,
    AuthMetrics metrics)
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

        var now = timeProvider.GetUtcNow();

        if (user.LockoutEndsAt is { } lockoutEndsAt)
        {
            if (lockoutEndsAt > now)
            {
                // The account is already locked. Do not advance the window and do not tell
                // the caller to write another account_locked audit event.
                return false;
            }

            // An expired lock grants a fresh allowance. The threshold is retained while the
            // lock is active so the stored state explains why the lock exists, then cleared
            // on the first attempt after expiry.
            user.FailedLoginCount = 0;
            user.LockoutEndsAt = null;
        }

        user.FailedLoginCount++;

        if (user.FailedLoginCount < _options.MaxFailedAttempts)
        {
            return false;
        }

        user.LockoutEndsAt = now.Add(_options.LockoutDuration);
        metrics.RecordLockout();
        return true;
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
