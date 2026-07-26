using Api.Attributes;
using Api.DTOs.Mfa;
using Api.Models.Enums;
using Api.Services.Mfa;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

/// <summary>TOTP enrolment and recovery codes.</summary>
[Route("api/v{version:apiVersion}/mfa")]
[Authorize]
public sealed class MfaController(
    ITotpService totpService,
    IRecoveryCodeService recoveryCodeService) : ApiControllerBase
{
    /// <summary>Starts TOTP enrolment. Returns the shared secret — once, and only here.</summary>
    [HttpPost("totp/enroll")]
    [ProducesResponseType<TotpEnrollmentResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<TotpEnrollmentResponse>> Enroll(
        CancellationToken cancellationToken) =>
        Ok(await totpService.EnrollAsync(CurrentUserId, cancellationToken));

    /// <summary>Confirms enrolment with a code from the authenticator, and issues recovery codes.</summary>
    /// <remarks>
    /// Until this succeeds the credential is unconfirmed and does not gate login — a failed
    /// scan must not lock a user out of their own account.
    /// </remarks>
    [HttpPost("totp/confirm")]
    [AuditEvent(AuditEventType.MfaEnrolled)]
    [ProducesResponseType<RecoveryCodesResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<RecoveryCodesResponse>> Confirm(
        [FromBody] ConfirmTotpRequest request,
        CancellationToken cancellationToken) =>
        Ok(await totpService.ConfirmAsync(CurrentUserId, request.Code, cancellationToken));

    /// <summary>Disables TOTP. <b>Requires recent authentication.</b></summary>
    /// <remarks>
    /// Removing a second factor is what an attacker does after stealing a live session, so a
    /// valid access token alone must not authorise it (Authentication.md §14). Outside the
    /// window this returns <c>403</c> with a step-up Problem Details type, not <c>401</c> —
    /// so a client can prompt for re-authentication rather than log the user out.
    /// </remarks>
    [HttpDelete("totp")]
    [RequireRecentAuth]
    [AuditEvent(AuditEventType.MfaDisabled)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult> DisableTotp(CancellationToken cancellationToken)
    {
        await totpService.DisableAsync(CurrentUserId, cancellationToken);
        return NoContent();
    }

    /// <summary>Replaces the recovery-code batch. <b>Requires recent authentication.</b></summary>
    /// <remarks>
    /// Silently invalidates codes the user may have printed, which is why it is step-up
    /// protected: an attacker would use it to strip the real owner's fallback.
    /// </remarks>
    [HttpPost("recovery-codes/regenerate")]
    [RequireRecentAuth]
    [ProducesResponseType<RecoveryCodesResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<RecoveryCodesResponse>> RegenerateRecoveryCodes(
        CancellationToken cancellationToken) =>
        Ok(await recoveryCodeService.RegenerateAsync(CurrentUserId, cancellationToken));
}
