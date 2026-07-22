using Api.DTOs.EmailVerification;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

/// <summary>Sending and confirming email-verification tokens.</summary>
[Route("api/v{version:apiVersion}/email-verification")]
public sealed class EmailVerificationController : ApiControllerBase
{
    /// <summary>Sends a fresh verification email to the authenticated user's address.</summary>
    /// <remarks>
    /// <c>202</c>, not <c>200</c>: the response does not depend on the email actually being
    /// delivered. Waiting on the provider would make delivery latency observable, and
    /// failures the client cannot act on anyway.
    /// </remarks>
    [HttpPost("send")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public Task<ActionResult> Send(CancellationToken cancellationToken) =>
        NotImplementedYetResult();

    /// <summary>Confirms an address with the token from the email.</summary>
    [HttpPost("confirm")]
    [AllowAnonymous]
    [ProducesResponseType<EmailVerifiedResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public Task<ActionResult<EmailVerifiedResponse>> Confirm(
        [FromBody] ConfirmEmailRequest request,
        CancellationToken cancellationToken) =>
        NotImplementedYet<EmailVerifiedResponse>();
}
