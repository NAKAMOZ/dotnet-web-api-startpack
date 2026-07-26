using Api.Attributes;
using Api.DTOs.Sessions;
using Api.Handlers.Authorization;
using Api.Models.Enums;
using Api.Services.Users;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

/// <summary>Administrative session revocation — the incident-response lever.</summary>
[Route("api/v{version:apiVersion}/admin/users/{userId:guid}/sessions")]
public sealed class AdminUserSessionsController(IAdminSessionService adminSessionService)
    : ApiControllerBase
{
    /// <summary>Revokes every session for one user.</summary>
    /// <remarks>
    /// Access tokens already issued stay cryptographically valid until they expire — at most
    /// fifteen minutes. That bound is the accepted cost of stateless validation
    /// (Authentication.md §13); an admin acting on a compromise should know it rather than
    /// assume the lockout is instant.
    /// </remarks>
    [HttpDelete]
    [RequirePermission(Permissions.SessionsRevokeAny)]
    [AuditEvent(AuditEventType.SessionRevoked)]
    [ProducesResponseType<RevokeSessionsResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RevokeSessionsResponse>> RevokeAll(
        Guid userId,
        CancellationToken cancellationToken) =>
        Ok(await adminSessionService.RevokeAllAsync(userId, cancellationToken));
}
