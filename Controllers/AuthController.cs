using Api.Configuration;
using Api.DTOs.Auth;
using Api.Services.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Api.Controllers;

/// <summary>Registration, login, MFA completion, refresh, logout and CSRF token issuance.</summary>
[Route("api/v{version:apiVersion}/auth")]
public sealed class AuthController(
    IRegistrationService registrationService,
    ILoginService loginService,
    IRefreshService refreshService,
    ILogoutService logoutService,
    IAuthTokenTransport tokenTransport) : ApiControllerBase
{
    /// <summary>Creates an account and sends a verification email.</summary>
    /// <remarks>
    /// Returns no tokens. The account exists but has not proven its address, and issuing a
    /// session here would make verification optional in practice.
    /// </remarks>
    [HttpPost("register")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.Registration)]
    [ProducesResponseType<RegisterResponse>(StatusCodes.Status202Accepted)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public Task<ActionResult<RegisterResponse>> Register(
        [FromBody] RegisterRequest request,
        CancellationToken cancellationToken) =>
        RegisterCoreAsync(request, cancellationToken);

    private async Task<ActionResult<RegisterResponse>> RegisterCoreAsync(
        RegisterRequest request,
        CancellationToken cancellationToken) =>
        Accepted(await registrationService.RegisterAsync(request, cancellationToken));

    /// <summary>
    /// Authenticates with email and password. Returns <c>202</c> with an MFA ticket when a
    /// second factor is enrolled.
    /// </summary>
    /// <remarks>
    /// Unknown email, wrong password and locked account all produce the <b>same</b> 401.
    /// The three cases are indistinguishable in body, code and timing (Authentication.md §5).
    /// </remarks>
    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.AuthStrict)]
    [ProducesResponseType<LoginResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<MfaChallengeResponse>(StatusCodes.Status202Accepted)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public Task<ActionResult<LoginResponse>> Login(
        [FromBody] LoginRequest request,
        CancellationToken cancellationToken) =>
        LoginCoreAsync(request, cancellationToken);

    /// <summary>Completes an MFA login with a TOTP or recovery code.</summary>
    [HttpPost("login/mfa")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.AuthStrict)]
    [ProducesResponseType<LoginResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<LoginResponse>> CompleteMfaLogin(
        [FromBody] MfaLoginRequest request,
        CancellationToken cancellationToken) =>
        Ok(await loginService.CompleteMfaAsync(request, cancellationToken));

    /// <summary>Rotates a refresh token, returning a new pair.</summary>
    /// <remarks>
    /// Anonymous by design: the refresh token <em>is</em> the credential. Presenting an
    /// already-used one revokes the whole session (Authentication.md §7).
    /// </remarks>
    [HttpPost("refresh")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.AuthStrict)]
    [ProducesResponseType<TokenPairResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<TokenPairResponse>> Refresh(
        [FromBody] RefreshRequest request,
        CancellationToken cancellationToken) =>
        Ok(await refreshService.RefreshAsync(request.RefreshToken, cancellationToken));

    /// <summary>Revokes the current session and clears the auth cookies.</summary>
    [HttpPost("logout")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult> Logout(CancellationToken cancellationToken)
    {
        await logoutService.LogoutAsync(CurrentUserId, CurrentSessionId, cancellationToken);
        return NoContent();
    }

    /// <summary>Issues a session-bound CSRF token and sets the readable CSRF cookie.</summary>
    [HttpGet("csrf")]
    [Authorize]
    [ProducesResponseType<CsrfTokenResponse>(StatusCodes.Status200OK)]
    public Task<ActionResult<CsrfTokenResponse>> GetCsrfToken(CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        return Task.FromResult<ActionResult<CsrfTokenResponse>>(
            Ok(tokenTransport.IssueCsrf(CurrentSessionId)));
    }

    private async Task<ActionResult<LoginResponse>> LoginCoreAsync(
        LoginRequest request,
        CancellationToken cancellationToken)
    {
        var result = await loginService.LoginAsync(request, cancellationToken);

        return result.Challenge is not null
            ? Accepted(result.Challenge)
            : Ok(result.Login);
    }
}
