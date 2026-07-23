using System.Security.Cryptography;
using System.Text;
using Api.Configuration;
using Api.Exceptions;
using Api.Handlers.Authentication;
using Api.Middleware;
using Api.Services.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Options;

namespace Api.Filters;

/// <summary>
/// Enforces the session-bound CSRF token on cookie-authenticated state-changing requests,
/// and exempts everything else (§14, Authentication.md §3).
/// </summary>
/// <remarks>
/// Global, and an authorization filter rather than an action filter: it runs before model
/// binding, so a forged request is rejected before its body is read.
/// <para>
/// <b>The exemption is the dangerous half.</b> Widening it — exempting by default, or
/// treating "no marker" as "bearer" for any reason other than the handler not having set one
/// — silently disables CSRF protection across the entire API while every test that checks a
/// happy path keeps passing. §22 asserts the filter fires for cookie-authenticated
/// state-changing requests, which is the only way that regression gets caught.
/// </para>
/// </remarks>
public sealed class CsrfProtectionFilter(
    ICsrfTokenService csrfTokenService,
    IOptions<AuthCookieOptions> cookieOptions) : IAuthorizationFilter
{
    private readonly AuthCookieOptions _cookies = cookieOptions.Value;

    public void OnAuthorization(AuthorizationFilterContext context)
    {
        var http = context.HttpContext;

        // Safe methods change nothing, so there is nothing to forge. This also keeps
        // GET /api/v1/auth/csrf — the endpoint that issues the token — reachable without
        // already holding one, which would otherwise be a deadlock.
        if (IsSafeMethod(http.Request.Method))
        {
            return;
        }

        // Anonymous requests carry no ambient credential to abuse: an attacker forging a
        // login has achieved nothing the browser could not do by navigating. Login CSRF
        // (forcing a victim into the attacker's session) is real but is answered by the
        // login response replacing the session, not by a token the caller does not yet have.
        if (http.User.Identity?.IsAuthenticated is not true)
        {
            return;
        }

        // Bearer tokens and API keys are supplied deliberately by the caller on each request.
        // A cross-origin page cannot make the browser attach either one, so those requests
        // are unreachable by CSRF and are exempt (Authentication.md §3).
        if (!http.Items.ContainsKey(AuthTransport.CookieAuthenticatedItemKey))
        {
            return;
        }

        var cookieToken = http.Request.Cookies[_cookies.CsrfCookieName];
        var headerToken = http.Request.Headers[_cookies.CsrfHeaderName].ToString();

        // Half one: the double submit. Proves the caller could read a cookie for this
        // origin, which a cross-origin page cannot do.
        if (!FixedTimeEquals(cookieToken, headerToken))
        {
            Reject(context, "The CSRF token is missing or does not match the CSRF cookie.");
            return;
        }

        // Half two: the binding. Proves the token was minted for the session this request
        // authenticated as, which a cookie-writing subdomain attacker cannot fake.
        if (!Guid.TryParse(http.User.FindFirst("sid")?.Value, out var sessionId)
            || !csrfTokenService.Validate(headerToken, sessionId))
        {
            Reject(context, "The CSRF token is not valid for this session.");
        }
    }

    private static bool IsSafeMethod(string method) =>
        HttpMethods.IsGet(method)
        || HttpMethods.IsHead(method)
        || HttpMethods.IsOptions(method)
        || HttpMethods.IsTrace(method);

    /// <summary>
    /// Constant-time comparison of two token strings, treating absence as mismatch.
    /// </summary>
    /// <remarks>
    /// Constant time matters here even though the token is not a password: an ordinary
    /// string comparison leaks a prefix at a time, and the caller controls both operands and
    /// can retry without limit.
    /// </remarks>
    private static bool FixedTimeEquals(string? left, string? right) =>
        !string.IsNullOrEmpty(left)
        && !string.IsNullOrEmpty(right)
        && CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(left),
            Encoding.UTF8.GetBytes(right));

    /// <summary>
    /// A 403 in this API's envelope.
    /// </summary>
    /// <remarks>
    /// The correlation and trace ids are attached here, not left to §13's
    /// <c>CustomizeProblemDetails</c>: a result written by a filter short-circuits the
    /// pipeline and never reaches <c>IProblemDetailsService</c> — the same reason
    /// <see cref="ValidationFilter"/> sets them itself.
    /// </remarks>
    private static void Reject(AuthorizationFilterContext context, string detail)
    {
        var problem = new ProblemDetails
        {
            Status = StatusCodes.Status403Forbidden,
            Title = "CSRF validation failed.",
            Type = ProblemTypes.For(ErrorCodes.CsrfValidationFailed),
            Detail = detail,
        };

        problem.Extensions[ProblemDetailsExtensions.ErrorCode] = ErrorCodes.CsrfValidationFailed;
        problem.Extensions[ProblemDetailsExtensions.TraceId] = context.HttpContext.TraceIdentifier;

        if (context.HttpContext.Items.TryGetValue(CorrelationId.ItemsKey, out var correlationId))
        {
            problem.Extensions[ProblemDetailsExtensions.CorrelationId] = correlationId;
        }

        context.Result = new ObjectResult(problem)
        {
            StatusCode = StatusCodes.Status403Forbidden,
            ContentTypes = { "application/problem+json" },
        };
    }
}
