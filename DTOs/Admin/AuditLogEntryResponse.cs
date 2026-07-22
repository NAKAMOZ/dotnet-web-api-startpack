using System.Text.Json;
using Api.Models.Enums;

namespace Api.DTOs.Admin;

/// <summary>One row of the security audit trail, for <c>GET /api/v1/admin/audit-logs</c>.</summary>
public sealed record AuditLogEntryResponse
{
    public required Guid Id { get; init; }

    /// <summary>Null for events with no identified actor — a failed login against an unknown address.</summary>
    public Guid? UserId { get; init; }

    public required AuditEventType EventType { get; init; }

    public string? IpAddress { get; init; }

    public string? UserAgent { get; init; }

    public string? CorrelationId { get; init; }

    /// <summary>
    /// Event-specific detail from the <c>jsonb</c> column.
    /// </summary>
    /// <remarks>
    /// Subject to the same redaction rules as logging (ADR-0010) — never a token, a
    /// password, a TOTP secret or key material. Serialising a whole request object into the
    /// audit table is how a credential ends up permanently stored in the one place nobody
    /// thinks to check.
    /// </remarks>
    public JsonElement? Metadata { get; init; }

    public required DateTimeOffset OccurredAt { get; init; }
}
