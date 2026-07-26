using Api.Configuration;
using Api.DTOs.Auth;
using Api.Services.Security;
using Microsoft.Extensions.Options;

namespace Api.Services.Auth;

public sealed class AuthTokenTransport(
    IHttpContextAccessor httpContextAccessor,
    ICsrfTokenService csrfTokenService,
    IOptions<AuthCookieOptions> cookieOptions) : IAuthTokenTransport
{
    private readonly AuthCookieOptions _cookies = cookieOptions.Value;

    public LoginResponse DeliverLogin(
        LoginResponse response,
        Guid sessionId,
        string accessToken,
        string refreshToken)
    {
        if (!UseCookies())
        {
            return response with { AccessToken = accessToken, RefreshToken = refreshToken };
        }

        WriteAuthCookies(sessionId, accessToken, refreshToken, response.ExpiresAt);
        return response with { AccessToken = null, RefreshToken = null };
    }

    public TokenPairResponse DeliverRefresh(
        TokenPairResponse response,
        Guid sessionId,
        string accessToken,
        string refreshToken)
    {
        if (!UseCookies())
        {
            return response with { AccessToken = accessToken, RefreshToken = refreshToken };
        }

        WriteAuthCookies(sessionId, accessToken, refreshToken, response.ExpiresAt);
        return response with { AccessToken = null, RefreshToken = null };
    }

    public string? ReadRefreshToken(string? bodyToken)
    {
        var cookieToken = Context.Request.Cookies[_cookies.RefreshCookieName];

        if (!string.IsNullOrEmpty(cookieToken)
            && !string.IsNullOrEmpty(bodyToken)
            && !string.Equals(cookieToken, bodyToken, StringComparison.Ordinal))
        {
            return null;
        }

        return cookieToken ?? bodyToken;
    }

    public CsrfTokenResponse IssueCsrf(Guid sessionId)
    {
        var token = csrfTokenService.Issue(sessionId);
        Context.Response.Cookies.Append(
            _cookies.CsrfCookieName,
            token,
            Cookie(httpOnly: false, SameSiteMode.Lax, "/"));

        return new CsrfTokenResponse { Token = token, HeaderName = _cookies.CsrfHeaderName };
    }

    public void ClearCookies()
    {
        Context.Response.Cookies.Delete(_cookies.AccessCookieName, Cookie(true, SameSiteMode.Lax, "/"));
        Context.Response.Cookies.Delete(
            _cookies.RefreshCookieName,
            Cookie(true, SameSiteMode.Strict, _cookies.RefreshCookiePath));
        Context.Response.Cookies.Delete(_cookies.CsrfCookieName, Cookie(false, SameSiteMode.Lax, "/"));
    }

    private HttpContext Context =>
        httpContextAccessor.HttpContext
        ?? throw new InvalidOperationException("Authentication transport requires an active HTTP request.");

    private bool UseCookies() =>
        string.Equals(
            Context.Request.Headers[_cookies.TransportHeaderName].ToString(),
            "cookie",
            StringComparison.OrdinalIgnoreCase)
        || Context.Request.Cookies.ContainsKey(_cookies.RefreshCookieName);

    private void WriteAuthCookies(
        Guid sessionId,
        string accessToken,
        string refreshToken,
        DateTimeOffset accessExpiresAt)
    {
        Context.Response.Cookies.Append(
            _cookies.AccessCookieName,
            accessToken,
            Cookie(true, SameSiteMode.Lax, "/", accessExpiresAt));
        Context.Response.Cookies.Append(
            _cookies.RefreshCookieName,
            refreshToken,
            Cookie(true, SameSiteMode.Strict, _cookies.RefreshCookiePath));
        _ = IssueCsrf(sessionId);
    }

    private CookieOptions Cookie(
        bool httpOnly,
        SameSiteMode sameSite,
        string path,
        DateTimeOffset? expires = null) =>
        new()
        {
            HttpOnly = httpOnly,
            Secure = _cookies.RequireSecure,
            SameSite = sameSite,
            Path = path,
            Expires = expires,
            IsEssential = true,
        };
}
