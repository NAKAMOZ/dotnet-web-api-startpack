namespace Api.Exceptions;

/// <summary>
/// The account is locked out after consecutive failures.
/// </summary>
/// <remarks>
/// <b>Never reaches the client as itself.</b> The login path converts it to
/// <see cref="InvalidCredentialsException"/> before responding — a distinct "account locked"
/// reply tells an attacker the address exists, and tells them their guessing is working.
/// <para>
/// It exists as a separate type so the <em>audit trail</em> can record
/// <c>account_locked</c> accurately (§15). Internal precision, external uniformity.
/// </para>
/// </remarks>
public sealed class AccountLockedException(DateTimeOffset lockoutEndsAt)
    : DomainException("account_locked", "The account is temporarily locked.")
{
    /// <summary>When the lock expires. For the audit record — never serialised to a client.</summary>
    public DateTimeOffset LockoutEndsAt { get; } = lockoutEndsAt;
}
