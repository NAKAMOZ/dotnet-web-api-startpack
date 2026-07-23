using System.Security.Claims;
using Api.Configuration;
using Api.Exceptions;
using Api.Filters;
using Api.Handlers.Authentication;
using Api.Services.Security;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;

namespace UnitTests.Security;

/// <summary>
/// The CSRF exemption matrix (§14, Authentication.md §3).
/// </summary>
/// <remarks>
/// Both directions are asserted, and the permissive one is the one that matters: a filter
/// that exempts too much protects nothing while every functional test stays green.
/// </remarks>
public class CsrfProtectionFilterTests
{
    private static readonly AuthCookieOptions Cookies = new();

    private readonly ICsrfTokenService _csrfTokenService = new CsrfTokenService(
        new EphemeralDataProtectionProvider(),
        Options.Create(new AuthSessionOptions()),
        TimeProvider.System);

    [Fact]
    public void CookieAuthenticatedChangeWithAValidTokenIsAllowed()
    {
        var sessionId = Guid.NewGuid();
        var token = _csrfTokenService.Issue(sessionId);

        var context = Request("POST", sessionId, cookieAuthenticated: true, cookie: token, header: token);

        Invoke(context);

        Assert.Null(context.Result);
    }

    [Fact]
    public void CookieAuthenticatedChangeWithNoHeaderIsRejected()
    {
        var sessionId = Guid.NewGuid();
        var token = _csrfTokenService.Issue(sessionId);

        var context = Request("POST", sessionId, cookieAuthenticated: true, cookie: token, header: null);

        Invoke(context);

        AssertRejected(context);
    }

    [Fact]
    public void CookieAuthenticatedChangeWithATokenFromAnotherSessionIsRejected()
    {
        // Cookie and header agree, so plain double-submit would pass. The session binding is
        // the only thing that fails this request.
        var token = _csrfTokenService.Issue(Guid.NewGuid());

        var context = Request("POST", Guid.NewGuid(), cookieAuthenticated: true, cookie: token, header: token);

        Invoke(context);

        AssertRejected(context);
    }

    [Fact]
    public void BearerAuthenticatedChangeWithNoTokenIsAllowed()
    {
        // A bearer token is attached deliberately by the caller; a cross-origin page cannot
        // make the browser send one. Nothing to protect against, so nothing to demand.
        var context = Request("POST", Guid.NewGuid(), cookieAuthenticated: false, cookie: null, header: null);

        Invoke(context);

        Assert.Null(context.Result);
    }

    [Theory]
    [InlineData("GET")]
    [InlineData("HEAD")]
    [InlineData("OPTIONS")]
    public void SafeMethodsAreExempt(string method)
    {
        // GET in particular must stay exempt or GET /api/v1/auth/csrf — the endpoint that
        // hands out the token — could never be called without already holding one.
        var context = Request(method, Guid.NewGuid(), cookieAuthenticated: true, cookie: null, header: null);

        Invoke(context);

        Assert.Null(context.Result);
    }

    [Fact]
    public void AnonymousChangeIsExempt()
    {
        // Login and registration are anonymous. There is no ambient credential to abuse yet.
        var httpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity()) };
        httpContext.Request.Method = "POST";

        var context = FilterContext(httpContext);

        Invoke(context);

        Assert.Null(context.Result);
    }

    private void Invoke(AuthorizationFilterContext context) =>
        new CsrfProtectionFilter(_csrfTokenService, Options.Create(Cookies)).OnAuthorization(context);

    private static void AssertRejected(AuthorizationFilterContext context)
    {
        var result = Assert.IsType<ObjectResult>(context.Result);
        var problem = Assert.IsType<ProblemDetails>(result.Value);

        Assert.Equal(StatusCodes.Status403Forbidden, result.StatusCode);
        Assert.Equal(
            ErrorCodes.CsrfValidationFailed,
            problem.Extensions[ProblemDetailsExtensions.ErrorCode]);
    }

    private static AuthorizationFilterContext Request(
        string method,
        Guid sessionId,
        bool cookieAuthenticated,
        string? cookie,
        string? header)
    {
        var identity = new ClaimsIdentity(
            [new Claim("sid", sessionId.ToString())],
            authenticationType: "Test");

        var httpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) };
        httpContext.Request.Method = method;

        if (cookieAuthenticated)
        {
            httpContext.Items[AuthTransport.CookieAuthenticatedItemKey] = true;
        }

        if (cookie is not null)
        {
            httpContext.Request.Headers.Cookie = $"{Cookies.CsrfCookieName}={cookie}";
        }

        if (header is not null)
        {
            httpContext.Request.Headers[Cookies.CsrfHeaderName] = header;
        }

        return FilterContext(httpContext);
    }

    private static AuthorizationFilterContext FilterContext(HttpContext httpContext) =>
        new(new ActionContext(httpContext, new RouteData(), new ActionDescriptor()), []);
}
