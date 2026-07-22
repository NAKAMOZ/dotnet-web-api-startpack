using Api.DTOs.Auth;
using Api.DTOs.SocialAuth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

/// <summary>Google and GitHub login through an API-driven redirect (ADR-0019).</summary>
[Route("api/v{version:apiVersion}/auth/social")]
[AllowAnonymous]
public sealed class SocialAuthController : ApiControllerBase
{
    /// <summary>Starts the OAuth flow — redirects to the provider with signed, single-use state.</summary>
    [HttpGet("{provider}/authorize")]
    [ProducesResponseType(StatusCodes.Status302Found)]
    [ProducesResponseType<SocialAuthorizeResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public Task<ActionResult<SocialAuthorizeResponse>> Authorize(
        string provider,
        CancellationToken cancellationToken) =>
        NotImplementedYet<SocialAuthorizeResponse>();

    /// <summary>Completes the flow: validates state, exchanges the code, creates a session.</summary>
    /// <remarks>
    /// An account is matched on <c>(provider, providerAccountId)</c> only — <b>never</b> on
    /// email alone, which would hand any account to whoever can get a provider to assert its
    /// address (Authentication.md §9).
    /// </remarks>
    [HttpGet("{provider}/callback")]
    [ProducesResponseType<LoginResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public Task<ActionResult<LoginResponse>> Callback(
        string provider,
        [FromQuery] SocialCallbackQuery query,
        CancellationToken cancellationToken) =>
        NotImplementedYet<LoginResponse>();
}
