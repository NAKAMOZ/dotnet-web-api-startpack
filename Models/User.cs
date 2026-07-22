namespace Api.Models;

/// <summary>
/// The account principal. Everything else in the model hangs off this row.
/// </summary>
public sealed class User : IAuditableEntity
{
    /// <summary>
    /// Guid v7 — time-ordered, so inserts append to the index instead of scattering across
    /// it the way v4 does. Assigned here rather than by the database; §7 configures
    /// <c>ValueGeneratedNever()</c>.
    /// </summary>
    public Guid Id { get; set; } = Guid.CreateVersion7();

    /// <summary>
    /// Login identity. Stored in a <c>citext</c> column with a unique index (§7), so
    /// case-insensitivity is a database property rather than something every query has to
    /// remember to apply.
    /// </summary>
    public required string Email { get; set; }

    /// <summary>
    /// Whether ownership of <see cref="Email"/> has been proven. Projected into the
    /// <c>email_verified</c> claim.
    /// </summary>
    /// <remarks>
    /// Set from a consumed <c>EmailVerification</c> token, or from a social login that
    /// actually asserts verification — Google (OIDC) does; GitHub does not, unless its
    /// email response marks the address both verified and primary (Authentication.md §9).
    /// </remarks>
    public bool EmailVerified { get; set; }

    /// <summary>
    /// Argon2id hash, with its parameters encoded inside the string so a stale hash can be
    /// detected and upgraded on the next successful login (ADR-0006).
    /// </summary>
    /// <remarks>
    /// <b>Nullable, and that is the design.</b> A social- or passkey-only account has no
    /// password; storing a random placeholder would mean the login path could not tell
    /// "this account has no password" from "this password is wrong", and password reset
    /// would silently create a credential the user never chose.
    /// </remarks>
    public string? PasswordHash { get; set; }

    /// <summary>Display name shown back to the user. The only profile field <c>PATCH /users/me</c> writes.</summary>
    public string? DisplayName { get; set; }

    /// <summary>
    /// Per-user kill switch. Rotated on password change and password reset; a session
    /// carries the value it saw at login, and refresh rejects any session whose snapshot no
    /// longer matches (Authentication.md §6).
    /// </summary>
    /// <remarks>
    /// Checked on refresh, not per request — that is what keeps access-token validation
    /// stateless while still bounding the blast radius to one access-token lifetime.
    /// </remarks>
    public string SecurityStamp { get; set; } = Guid.CreateVersion7().ToString("N");

    /// <summary>
    /// Consecutive failed logins. Reset to zero on success — a user who mistypes four times
    /// then succeeds must not be one mistake from lockout tomorrow (Authentication.md §5).
    /// </summary>
    public int FailedLoginCount { get; set; }

    /// <summary>
    /// When the current lockout ends, or null if not locked. Five failures locks for
    /// fifteen minutes (§16).
    /// </summary>
    /// <remarks>
    /// Never surfaced to the client. A locked account returns the same
    /// <c>invalid_credentials</c> response as a wrong password; a distinct "account locked"
    /// reply tells an attacker the address exists.
    /// </remarks>
    public DateTimeOffset? LockoutEndsAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>Live and historical logins. Queried by the session-list endpoint.</summary>
    public ICollection<Session> Sessions { get; } = [];

    /// <summary>Linked external identities. Queried by <c>GET /users/me/accounts</c>.</summary>
    public ICollection<Account> Accounts { get; } = [];

    /// <summary>Role assignments. Loaded on login to build the <c>roles</c> claim.</summary>
    public ICollection<UserRole> UserRoles { get; } = [];

    /// <summary>MFA fallback codes. Null-navigation is normal — most users have none.</summary>
    public ICollection<RecoveryCode> RecoveryCodes { get; } = [];

    public ICollection<PasskeyCredential> PasskeyCredentials { get; } = [];

    public ICollection<ApiKey> ApiKeys { get; } = [];

    /// <summary>At most one authenticator secret per user (unique index on <c>UserId</c>, §7).</summary>
    public TotpCredential? TotpCredential { get; set; }
}
