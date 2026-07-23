namespace Api.Configuration;

/// <summary>
/// The authentication cookie matrix — see
/// <c>Documentation/Architecture/Authentication.md</c> §3 and ADR-0003.
/// <para>
/// Named <c>AuthCookieOptions</c> rather than the roadmap's <c>CookieOptions</c>:
/// <see cref="Microsoft.AspNetCore.Http.CookieOptions"/> already exists and is in scope
/// through implicit usings, so the roadmap's name would collide wherever both are used.
/// </para>
/// <para>
/// This is load-bearing security configuration, not preference. It is validated at
/// startup (§25) so a misconfigured policy fails the process at boot rather than
/// producing a subtly insecure runtime.
/// </para>
/// </summary>
public sealed class AuthCookieOptions
{
    public const string SectionName = "AuthCookies";

    /// <summary>
    /// Access-token cookie. The <c>__Host-</c> prefix requires <c>Secure</c>, <c>Path=/</c>
    /// and no <c>Domain</c> — the browser enforces those, which is why the prefix is worth
    /// having.
    /// </summary>
    public string AccessCookieName { get; init; } = "__Host-auth.access";

    /// <summary>
    /// Refresh-token cookie. <c>__Secure-</c> rather than <c>__Host-</c> because
    /// <c>__Host-</c> mandates <c>Path=/</c>, which is incompatible with scoping this
    /// cookie to the refresh endpoint. Path scoping was judged the more valuable property:
    /// the browser then never attaches the refresh token anywhere else.
    /// </summary>
    public string RefreshCookieName { get; init; } = "__Secure-auth.refresh";

    /// <summary>
    /// CSRF cookie. Deliberately <b>not</b> <c>httpOnly</c> — double-submit requires
    /// JavaScript to read it and echo it in <c>X-CSRF-Token</c>.
    /// </summary>
    public string CsrfCookieName { get; init; } = "__Host-auth.csrf";

    /// <summary>
    /// Path the refresh cookie is scoped to. Must match the refresh endpoint route, or the
    /// browser will not send the cookie and every refresh fails.
    /// </summary>
    public string RefreshCookiePath { get; init; } = "/api/v1/auth/refresh";

    /// <summary>
    /// Header a client sets on login to choose the transport. Default is body; the server
    /// never issues tokens in both cookie and body at once.
    /// </summary>
    public string TransportHeaderName { get; init; } = AuthCookieDefaults.TransportHeaderName;

    /// <summary>Header carrying the echoed CSRF token in cookie mode.</summary>
    public string CsrfHeaderName { get; init; } = AuthCookieDefaults.CsrfHeaderName;

    /// <summary>
    /// Whether <c>Secure</c> is required. Always true outside Development. Note that
    /// leaving it true — the correct setting — means cookie mode does not work over plain
    /// HTTP, so browser flows must be tested against the HTTPS profile.
    /// </summary>
    public bool RequireSecure { get; init; } = true;
}
