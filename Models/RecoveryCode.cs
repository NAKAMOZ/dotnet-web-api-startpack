namespace Api.Models;

/// <summary>
/// One single-use MFA fallback code. A batch is issued at enrolment and shown once;
/// regenerating replaces the whole batch.
/// </summary>
public sealed class RecoveryCode : IAuditableEntity
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid UserId { get; set; }

    public User User { get; set; } = null!;

    /// <summary>
    /// Hash of the code. These are generated, high-entropy values, so the cheap hash
    /// profile applies — the same reasoning as API keys (Authentication.md §15), and the
    /// same warning: that profile must never touch a user-chosen password.
    /// </summary>
    public required string CodeHash { get; set; }

    /// <summary>
    /// When the code was spent. Spent codes are <b>kept</b>, not deleted: a login with
    /// <c>amr = [pwd, recovery]</c> is a security event, and deleting the row erases the
    /// evidence that it happened.
    /// </summary>
    public DateTimeOffset? UsedAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
