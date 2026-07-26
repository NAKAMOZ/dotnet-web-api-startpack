using Api.Attributes;
using Api.Configuration;
using Api.DTOs.Auth;
using Api.DTOs.Passkeys;
using Api.Models.Enums;
using Api.Services.Passkeys;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Api.Controllers;

/// <summary>WebAuthn registration and authentication ceremonies.</summary>
[Route("api/v{version:apiVersion}/passkeys")]
[Authorize]
public sealed class PasskeysController(IPasskeyService passkeyService) : ApiControllerBase
{
    /// <summary>Creation options for a new credential.</summary>
    [HttpPost("registration/options")]
    [ProducesResponseType<PasskeyRegistrationOptionsResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<PasskeyRegistrationOptionsResponse>> RegistrationOptions(
        [FromBody] PasskeyRegistrationOptionsRequest request,
        CancellationToken cancellationToken) =>
        Ok(await passkeyService.RegistrationOptionsAsync(CurrentUserId, request, cancellationToken));

    /// <summary>Verifies the attestation and stores the credential.</summary>
    /// <remarks>
    /// Verified against the <b>stored</b> challenge, never one echoed back by the client — a
    /// ceremony that trusts the client's copy verifies nothing.
    /// </remarks>
    [HttpPost("registration/complete")]
    [AuditEvent(AuditEventType.PasskeyRegistered)]
    [ProducesResponseType<PasskeyResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<PasskeyResponse>> CompleteRegistration(
        [FromBody] PasskeyRegistrationRequest request,
        CancellationToken cancellationToken) =>
        StatusCode(
            StatusCodes.Status201Created,
            await passkeyService.CompleteRegistrationAsync(CurrentUserId, request, cancellationToken));

    /// <summary>Request options for an assertion. Anonymous — this is a login path.</summary>
    /// <remarks>
    /// The response must look identical whether or not the hinted address exists, or this
    /// endpoint becomes an anonymous account-enumeration oracle.
    /// </remarks>
    [HttpPost("authentication/options")]
    [AllowAnonymous]
    [ProducesResponseType<PasskeyAuthenticationOptionsResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PasskeyAuthenticationOptionsResponse>> AuthenticationOptions(
        [FromBody] PasskeyAuthenticationOptionsRequest request,
        CancellationToken cancellationToken)
    {
        _ = request;
        return Ok(await passkeyService.AuthenticationOptionsAsync(cancellationToken));
    }

    /// <summary>Verifies an assertion and creates a session with <c>amr: [webauthn]</c>.</summary>
    [HttpPost("authentication/complete")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.AuthStrict)]
    [ProducesResponseType<LoginResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<LoginResponse>> CompleteAuthentication(
        [FromBody] PasskeyAuthenticationRequest request,
        CancellationToken cancellationToken) =>
        Ok(await passkeyService.CompleteAuthenticationAsync(request, cancellationToken));

    /// <summary>Lists the caller's registered credentials.</summary>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<PasskeyResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IReadOnlyList<PasskeyResponse>>> List(
        CancellationToken cancellationToken) =>
        Ok(await passkeyService.ListAsync(CurrentUserId, cancellationToken));

    /// <summary>Removes one credential.</summary>
    /// <remarks>
    /// Scoped to the caller in the same query — <c>WHERE CredentialId = @id AND UserId = @sub</c>.
    /// Fetch-then-compare is the classic IDOR shape (Authorization.md §5).
    /// </remarks>
    [HttpDelete("{credentialId}")]
    [AuditEvent(AuditEventType.PasskeyRemoved)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> Remove(
        string credentialId,
        CancellationToken cancellationToken)
    {
        await passkeyService.RemoveAsync(CurrentUserId, credentialId, cancellationToken);
        return NoContent();
    }
}
