using Api.Attributes;
using Api.DTOs.Admin;
using Api.DTOs.Common;
using Api.Handlers.Authorization;
using Api.Models.Enums;
using Api.Services.Users;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

/// <summary>Administrative access to any user account.</summary>
/// <remarks>
/// Split from the role and session admin controllers on purpose: each permission in the
/// catalog is distinct, and a controller that accumulates unrelated administrative
/// operations ends up carrying the union of their permissions.
/// </remarks>
[Route("api/v{version:apiVersion}/admin/users")]
public sealed class AdminUsersController(IAdminUserService adminUserService) : ApiControllerBase
{
    /// <summary>Lists users, paged and filterable.</summary>
    /// <remarks>Sort fields come from an allow-list — an unrestricted sort orders by columns the caller cannot read.</remarks>
    [HttpGet]
    [RequirePermission(Permissions.UsersReadAny)]
    [ProducesResponseType<PagedResponse<AdminUserResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<PagedResponse<AdminUserResponse>>> List(
        [FromQuery] AdminUserListQuery query,
        CancellationToken cancellationToken) =>
        Ok(await adminUserService.ListAsync(query, cancellationToken));

    /// <summary>One user in full, including lockout state and live sessions.</summary>
    [HttpGet("{userId:guid}")]
    [RequirePermission(Permissions.UsersReadAny)]
    [ProducesResponseType<AdminUserDetailResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AdminUserDetailResponse>> Get(
        Guid userId,
        CancellationToken cancellationToken) =>
        Ok(await adminUserService.GetAsync(userId, cancellationToken));

    /// <summary>Updates verification state, display name, or clears a lockout.</summary>
    /// <remarks>
    /// Cannot set a password and cannot impose a lockout. An admin-set password is a
    /// credential the user did not choose and the admin knows; lockout is a consequence of
    /// failed logins, not a switch.
    /// </remarks>
    [HttpPatch("{userId:guid}")]
    [RequirePermission(Permissions.UsersWriteAny)]
    [RequireRecentAuth]
    [AuditEvent(AuditEventType.AdminUserUpdated)]
    [ProducesResponseType<AdminUserDetailResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AdminUserDetailResponse>> Update(
        Guid userId,
        [FromBody] AdminUpdateUserRequest request,
        CancellationToken cancellationToken) =>
        Ok(await adminUserService.UpdateAsync(userId, request, cancellationToken));

    /// <summary>Deletes a user and every credential they hold. The audit trail survives.</summary>
    /// <remarks>
    /// <b>Deliberately not marked with <c>[AuditEvent]</c>,</b> unlike the other mutating admin
    /// actions. <c>AuditActionFilter</c> runs after the action and records the route's user id
    /// as the row's subject — and by then that user is gone. <c>AuditLogEntry.UserId</c> is a
    /// foreign key, so the insert would violate it and the one event nobody can afford to lose
    /// would be the one that fails to write. §12's deletion service records
    /// <c>admin_user_deleted</c> from inside its own path, with a null subject and the deleted
    /// id in the metadata (AuditTrail.md §4).
    /// </remarks>
    [HttpDelete("{userId:guid}")]
    [RequirePermission(Permissions.UsersDeleteAny)]
    [RequireRecentAuth]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> Delete(Guid userId, CancellationToken cancellationToken)
    {
        await adminUserService.DeleteAsync(userId, cancellationToken);
        return NoContent();
    }
}
