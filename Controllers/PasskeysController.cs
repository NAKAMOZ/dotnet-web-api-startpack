using Api.DTOs.Auth;
using Api.DTOs.Passkeys;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

/// <summary>WebAuthn registration and authentication ceremonies.</summary>
[Route("api/v{version:apiVersion}/passkeys")]
[Authorize]
public sealed class PasskeysController : ApiControllerBase
{
    /// <summary>Creation options for a new credential.</summary>
    [HttpPost("registration/options")]
    [ProducesResponseType<PasskeyRegistrationOptionsResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public Task<ActionResult<PasskeyRegistrationOptionsResponse>> RegistrationOptions(
        [FromBody] PasskeyRegistrationOptionsRequest request,
        CancellationToken cancellationToken) =>
        NotImplementedYet<PasskeyRegistrationOptionsResponse>();

    /// <summary>Verifies the attestation and stores the credential.</summary>
    /// <remarks>
    /// Verified against the <b>stored</b> challenge, never one echoed back by the client — a
    /// ceremony that trusts the client's copy verifies nothing.
    /// </remarks>
    [HttpPost("registration/complete")]
    [ProducesResponseType<PasskeyResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public Task<ActionResult<PasskeyResponse>> CompleteRegistration(
        [FromBody] PasskeyRegistrationRequest request,
        CancellationToken cancellationToken) =>
        NotImplementedYet<PasskeyResponse>();

    /// <summary>Request options for an assertion. Anonymous — this is a login path.</summary>
    /// <remarks>
    /// The response must look identical whether or not the hinted address exists, or this
    /// endpoint becomes an anonymous account-enumeration oracle.
    /// </remarks>
    [HttpPost("authentication/options")]
    [AllowAnonymous]
    [ProducesResponseType<PasskeyAuthenticationOptionsResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public Task<ActionResult<PasskeyAuthenticationOptionsResponse>> AuthenticationOptions(
        [FromBody] PasskeyAuthenticationOptionsRequest request,
        CancellationToken cancellationToken) =>
        NotImplementedYet<PasskeyAuthenticationOptionsResponse>();

    /// <summary>Verifies an assertion and creates a session with <c>amr: [webauthn]</c>.</summary>
    [HttpPost("authentication/complete")]
    [AllowAnonymous]
    [ProducesResponseType<LoginResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public Task<ActionResult<LoginResponse>> CompleteAuthentication(
        [FromBody] PasskeyAuthenticationRequest request,
        CancellationToken cancellationToken) =>
        NotImplementedYet<LoginResponse>();

    /// <summary>Lists the caller's registered credentials.</summary>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<PasskeyResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public Task<ActionResult<IReadOnlyList<PasskeyResponse>>> List(CancellationToken cancellationToken) =>
        NotImplementedYet<IReadOnlyList<PasskeyResponse>>();

    /// <summary>Removes one credential.</summary>
    /// <remarks>
    /// Scoped to the caller in the same query — <c>WHERE CredentialId = @id AND UserId = @sub</c>.
    /// Fetch-then-compare is the classic IDOR shape (Authorization.md §5).
    /// </remarks>
    [HttpDelete("{credentialId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public Task<ActionResult> Remove(string credentialId, CancellationToken cancellationToken) =>
        NotImplementedYetResult();
}
