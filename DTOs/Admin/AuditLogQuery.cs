using Api.DTOs.Common;
using Api.Models.Enums;

namespace Api.DTOs.Admin;

/// <summary>Filters for <c>GET /api/v1/admin/audit-logs</c>.</summary>
/// <remarks>
/// Each filter matches an index on the table (DataAccess.md §3) — the query surface and the
/// index set were designed against each other rather than one being retrofitted to the
/// other.
/// </remarks>
public sealed record AuditLogQuery : PagedQuery
{
    /// <summary>Subject of the event. Null rows — events with no identified actor — are excluded when set.</summary>
    public Guid? UserId { get; init; }

    /// <summary>
    /// Bound to the enum, not to a free string. An unknown event type is then a 400 rather
    /// than an empty result that reads like "this never happened".
    /// </summary>
    public AuditEventType? EventType { get; init; }

    public DateTimeOffset? From { get; init; }

    public DateTimeOffset? To { get; init; }

    /// <summary>Stitches an audit row to the operational log lines from the same request.</summary>
    public string? CorrelationId { get; init; }
}
