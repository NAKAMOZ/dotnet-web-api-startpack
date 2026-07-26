using Api.Attributes;
using Api.DTOs.Admin;
using Api.Handlers.Authorization;
using Api.Models.Enums;
using Api.Services.Users;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

/// <summary>Role grants and revocations.</summary>
/// <remarks>
/// Separate from <c>AdminUsersController</c> because granting a role and editing a profile
/// are separate permissions — <c>roles:assign</c> and <c>roles:revoke</c> are also separate
/// from each other, so that revoking can be delegated without granting.
/// </remarks>
[Route("api/v{version:apiVersion}/admin/users/{userId:guid}/roles")]
public sealed class AdminUserRolesController(IAdminRoleService adminRoleService) : ApiControllerBase
{
    /// <summary>Grants a role.</summary>
    /// <remarks>
    /// The grant takes effect for the target user on their next token issuance, not
    /// immediately: roles ride in the access token, which stays valid until it expires —
    /// at most fifteen minutes (Authentication.md §13).
    /// </remarks>
    [HttpPost]
    [RequirePermission(Permissions.RolesAssign)]
    [AuditEvent(AuditEventType.RoleGranted)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> Grant(
        Guid userId,
        [FromBody] AssignRoleRequest request,
        CancellationToken cancellationToken)
    {
        await adminRoleService.GrantAsync(userId, request.RoleId, cancellationToken);
        return NoContent();
    }

    /// <summary>Revokes a role.</summary>
    [HttpDelete("{roleId:guid}")]
    [RequirePermission(Permissions.RolesRevoke)]
    [AuditEvent(AuditEventType.RoleRevoked)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> Revoke(
        Guid userId,
        Guid roleId,
        CancellationToken cancellationToken)
    {
        await adminRoleService.RevokeAsync(userId, roleId, cancellationToken);
        return NoContent();
    }
}
