using Api.DTOs.Admin;
using Api.DTOs.Common;

namespace Api.Services.Audit;

/// <summary>
/// Reads the security audit trail for <c>GET /api/v1/admin/audit-logs</c> (§15).
/// </summary>
/// <remarks>
/// Separate from <see cref="IAuditLogger"/> so the write path cannot read. §12's services
/// depend on the logger; nothing but the admin controller depends on this.
/// <para>
/// There is no update and no delete, here or anywhere: retention is a background job (§12)
/// operating on the table, not an API surface. A trail an operator can edit answers a
/// different question than the one it was kept for.
/// </para>
/// </remarks>
public interface IAuditQueryService
{
    Task<PagedResponse<AuditLogEntryResponse>> QueryAsync(
        AuditLogQuery query,
        CancellationToken cancellationToken = default);
}
