using Api.Attributes;
using Api.DTOs.Admin;
using Api.DTOs.Common;
using Api.Handlers.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

/// <summary>Read access to the security audit trail.</summary>
/// <remarks>
/// Read-only by construction: there is no endpoint that updates or deletes an audit row.
/// Retention is a background job (§12), not an API surface — a trail an operator can edit
/// answers a different question than the one it was kept for.
/// </remarks>
[Route("api/v{version:apiVersion}/admin/audit-logs")]
public sealed class AdminAuditLogsController : ApiControllerBase
{
    /// <summary>Queries audit events by user, type, date range or correlation id.</summary>
    [HttpGet]
    [RequirePermission(Permissions.AuditRead)]
    [ProducesResponseType<PagedResponse<AuditLogEntryResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public Task<ActionResult<PagedResponse<AuditLogEntryResponse>>> Query(
        [FromQuery] AuditLogQuery query,
        CancellationToken cancellationToken) =>
        NotImplementedYet<PagedResponse<AuditLogEntryResponse>>();
}
