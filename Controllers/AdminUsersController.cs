using Api.Attributes;
using Api.DTOs.Admin;
using Api.DTOs.Common;
using Api.Handlers.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

/// <summary>Administrative access to any user account.</summary>
/// <remarks>
/// Split from the role and session admin controllers on purpose: each permission in the
/// catalog is distinct, and a controller that accumulates unrelated administrative
/// operations ends up carrying the union of their permissions.
/// </remarks>
[Route("api/v{version:apiVersion}/admin/users")]
public sealed class AdminUsersController : ApiControllerBase
{
    /// <summary>Lists users, paged and filterable.</summary>
    /// <remarks>Sort fields come from an allow-list — an unrestricted sort orders by columns the caller cannot read.</remarks>
    [HttpGet]
    [RequirePermission(Permissions.UsersReadAny)]
    [ProducesResponseType<PagedResponse<AdminUserResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public Task<ActionResult<PagedResponse<AdminUserResponse>>> List(
        [FromQuery] AdminUserListQuery query,
        CancellationToken cancellationToken) =>
        NotImplementedYet<PagedResponse<AdminUserResponse>>();

    /// <summary>One user in full, including lockout state and live sessions.</summary>
    [HttpGet("{userId:guid}")]
    [RequirePermission(Permissions.UsersReadAny)]
    [ProducesResponseType<AdminUserDetailResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public Task<ActionResult<AdminUserDetailResponse>> Get(Guid userId, CancellationToken cancellationToken) =>
        NotImplementedYet<AdminUserDetailResponse>();

    /// <summary>Updates verification state, display name, or clears a lockout.</summary>
    /// <remarks>
    /// Cannot set a password and cannot impose a lockout. An admin-set password is a
    /// credential the user did not choose and the admin knows; lockout is a consequence of
    /// failed logins, not a switch.
    /// </remarks>
    [HttpPatch("{userId:guid}")]
    [RequirePermission(Permissions.UsersWriteAny)]
    [ProducesResponseType<AdminUserDetailResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public Task<ActionResult<AdminUserDetailResponse>> Update(
        Guid userId,
        [FromBody] AdminUpdateUserRequest request,
        CancellationToken cancellationToken) =>
        NotImplementedYet<AdminUserDetailResponse>();

    /// <summary>Deletes a user and every credential they hold. The audit trail survives.</summary>
    [HttpDelete("{userId:guid}")]
    [RequirePermission(Permissions.UsersDeleteAny)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public Task<ActionResult> Delete(Guid userId, CancellationToken cancellationToken) =>
        NotImplementedYetResult();
}
