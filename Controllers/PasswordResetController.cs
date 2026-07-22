using Api.DTOs.PasswordReset;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

/// <summary>The forgotten-password flow. Both endpoints are anonymous by necessity.</summary>
[Route("api/v{version:apiVersion}/password-reset")]
[AllowAnonymous]
public sealed class PasswordResetController : ApiControllerBase
{
    /// <summary>Requests a reset email.</summary>
    /// <remarks>
    /// <b>Always <c>202</c></b> for a well-formed address, whether or not an account exists.
    /// A 404 for unknown addresses would be an account-enumeration oracle needing no
    /// credentials at all.
    /// </remarks>
    [HttpPost("request")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    // Named RequestReset, not Request: an action called Request hides ControllerBase.Request
    // (the HttpRequest accessor), which is a compile error here and would be a subtle bug in
    // any controller that used both.
    public Task<ActionResult> RequestReset(
        [FromBody] PasswordResetRequest request,
        CancellationToken cancellationToken) =>
        NotImplementedYetResult();

    /// <summary>Sets a new password using the emailed token.</summary>
    /// <remarks>
    /// Bumps <c>SecurityStamp</c> and revokes <b>every</b> session — a reset exists precisely
    /// for the case where someone else may hold a live one (Authentication.md §13).
    /// </remarks>
    [HttpPost("confirm")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public Task<ActionResult> Confirm(
        [FromBody] PasswordResetConfirmRequest request,
        CancellationToken cancellationToken) =>
        NotImplementedYetResult();
}
