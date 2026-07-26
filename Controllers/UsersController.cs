using Api.Attributes;
using Api.DTOs.Users;
using Api.Services.Users;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

/// <summary>
/// Self-service account management. Every route is <c>/me</c> — none takes a user id, which
/// is why none of them has an ownership check to get wrong (Authorization.md §5).
/// </summary>
[Route("api/v{version:apiVersion}/users/me")]
[Authorize]
public sealed class UsersController(IUserService userService) : ApiControllerBase
{
    /// <summary>The caller's own profile.</summary>
    [HttpGet]
    [ProducesResponseType<UserProfileResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<UserProfileResponse>> GetProfile(
        CancellationToken cancellationToken) =>
        Ok(await userService.GetProfileAsync(CurrentUserId, cancellationToken));

    /// <summary>Updates the display name — the only mutable profile field.</summary>
    [HttpPatch]
    [ProducesResponseType<UserProfileResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<UserProfileResponse>> UpdateProfile(
        [FromBody] UpdateProfileRequest request,
        CancellationToken cancellationToken) =>
        Ok(await userService.UpdateProfileAsync(CurrentUserId, request, cancellationToken));

    /// <summary>Deletes the account irreversibly. <b>Requires recent authentication.</b></summary>
    /// <remarks>
    /// Cascades to every credential the account holds — deleting an account must destroy
    /// every way of authenticating as it. The audit trail survives (DataAccess.md §4).
    /// </remarks>
    [HttpDelete]
    [RequireRecentAuth]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult> DeleteAccount(CancellationToken cancellationToken)
    {
        await userService.DeleteAccountAsync(CurrentUserId, cancellationToken);
        return NoContent();
    }

    /// <summary>Changes the password, then revokes every session including this one.</summary>
    /// <remarks>
    /// Deliberately <b>not</b> step-up protected: it requires the current password, which is
    /// stronger proof than a recent-auth timestamp. Demanding both would mean
    /// re-authenticating in order to re-authenticate (Authentication.md §14).
    /// </remarks>
    [HttpPut("password")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult> ChangePassword(
        [FromBody] ChangePasswordRequest request,
        CancellationToken cancellationToken)
    {
        await userService.ChangePasswordAsync(
            CurrentUserId,
            CurrentSessionId,
            request,
            cancellationToken);
        return NoContent();
    }

    /// <summary>Lists linked social accounts.</summary>
    [HttpGet("accounts")]
    [ProducesResponseType<IReadOnlyList<LinkedAccountResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IReadOnlyList<LinkedAccountResponse>>> ListAccounts(
        CancellationToken cancellationToken) =>
        Ok(await userService.ListAccountsAsync(CurrentUserId, cancellationToken));

    /// <summary>Unlinks a social account.</summary>
    /// <remarks>
    /// Refused when it would leave the account with no way to authenticate at all — no
    /// password, no passkey, and no remaining provider.
    /// </remarks>
    [HttpDelete("accounts/{accountId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult> UnlinkAccount(
        Guid accountId,
        CancellationToken cancellationToken)
    {
        await userService.UnlinkAccountAsync(CurrentUserId, accountId, cancellationToken);
        return NoContent();
    }
}
