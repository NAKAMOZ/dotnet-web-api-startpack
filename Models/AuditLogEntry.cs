using Api.Models.Enums;

namespace Api.Models;

/// <summary>
/// One row of the security audit trail — a queryable record that outlives log rotation
/// (§15).
/// </summary>
/// <remarks>
/// <b>Deliberately not <see cref="IAuditableEntity"/>.</b> Audit rows are append-only: they
/// are never updated, so an <c>UpdatedAt</c> column here would have no honest meaning and
/// would suggest a write path that must not exist. <see cref="OccurredAt"/> replaces both
/// stamps and is set from <see cref="TimeProvider"/> at the call site.
/// </remarks>
public sealed class AuditLogEntry
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    /// <summary>
    /// Subject of the event, when one is known. Null for events with no identified actor —
    /// a failed login against an address that does not exist, for instance.
    /// </summary>
    /// <remarks>
    /// The foreign key uses <c>SetNull</c> on delete, not cascade (§7): deleting an account
    /// must not erase the record of what it did. This is the one place where a
    /// <see cref="User"/> deletion deliberately does not take its data with it.
    /// </remarks>
    public Guid? UserId { get; set; }

    public required AuditEventType EventType { get; set; }

    public string? IpAddress { get; set; }

    public string? UserAgent { get; set; }

    /// <summary>
    /// Correlation ID of the request that produced the event (§14). What stitches an audit
    /// row to the operational log lines around it.
    /// </summary>
    public string? CorrelationId { get; set; }

    /// <summary>
    /// Event-specific detail, stored as <c>jsonb</c> (§7) so the admin query endpoint can
    /// filter inside it.
    /// </summary>
    /// <remarks>
    /// Subject to the same redaction rules as logging (ADR-0010): never a token, a password,
    /// a TOTP secret, or key material. Serialising a whole request object into here is how
    /// a credential ends up permanently stored in the one table nobody thinks to check.
    /// </remarks>
    public string? Metadata { get; set; }

    /// <summary>When the event happened, from the injected <see cref="TimeProvider"/>.</summary>
    public DateTimeOffset OccurredAt { get; set; }
}
