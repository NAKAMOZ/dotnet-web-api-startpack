using Api.Middleware;

namespace Api.Configuration;

/// <summary>
/// Cross-origin policy: which browser origins may call this API, and which of them may do
/// so with cookies attached (§14).
/// </summary>
/// <remarks>
/// Named <c>ApiCorsOptions</c> rather than the roadmap's <c>CorsOptions</c>, for the same
/// reason <see cref="AuthCookieOptions"/> is not <c>CookieOptions</c>:
/// <see cref="Microsoft.AspNetCore.Cors.Infrastructure.CorsOptions"/> already exists and is
/// used in the very file that configures this one.
/// <para>
/// <b>Both lists default to empty, which denies every cross-origin browser call.</b> An API
/// that is called only server-to-server needs no entries at all — CORS constrains browsers,
/// not servers — so an unconfigured deployment is closed rather than open.
/// </para>
/// </remarks>
public sealed class ApiCorsOptions
{
    public const string SectionName = "Cors";

    /// <summary>Policy name under which the resolved policy is registered.</summary>
    public const string PolicyName = "ApiDefault";

    /// <summary>
    /// Origins allowed to call the API with a bearer token — the tokens-in-the-body
    /// transport. Cookies are <b>not</b> permitted for these origins.
    /// </summary>
    /// <remarks>
    /// Exact origins (<c>https://app.example.com</c>), never <c>*</c>. A wildcard is
    /// incompatible with credentials by browser rule, and it also hands any page on the
    /// internet a scripted client for this API.
    /// </remarks>
    public string[] AllowedOrigins { get; init; } = [];

    /// <summary>
    /// Origins allowed to call the API in <b>cookie mode</b> — the only ones for which
    /// <c>Access-Control-Allow-Credentials: true</c> is ever emitted.
    /// </summary>
    /// <remarks>
    /// Kept separate from <see cref="AllowedOrigins"/> on purpose. Credentials plus a
    /// reflected origin is the classic CORS misconfiguration: it lets the listed page read
    /// authenticated responses, so the list must be the small set of first-party front ends
    /// that genuinely use cookies, not every origin that happens to be allowed.
    /// </remarks>
    public string[] CookieModeOrigins { get; init; } = [];

    /// <summary>Request headers a cross-origin caller may send.</summary>
    /// <remarks>
    /// Enumerated rather than <c>AllowAnyHeader</c> so a new client header is a deliberate
    /// change here instead of an accident. The CSRF and transport headers are listed because
    /// cookie mode does not function without them.
    /// </remarks>
    public string[] AllowedHeaders { get; init; } =
    [
        "Content-Type",
        "Authorization",
        "Accept",
        AuthCookieDefaults.CsrfHeaderName,
        AuthCookieDefaults.TransportHeaderName,
        CorrelationId.HeaderName,
    ];

    /// <summary>Response headers a cross-origin caller may read.</summary>
    /// <remarks>
    /// A browser exposes only a handful of response headers to script by default, and none
    /// of ours are among them. The correlation id has to be readable or a front end cannot
    /// put it in a bug report, which is most of its value.
    /// </remarks>
    public string[] ExposedHeaders { get; init; } =
    [
        CorrelationId.HeaderName,
        "Retry-After",
    ];

    /// <summary>
    /// How long a browser may cache a preflight result. Ten minutes: long enough to keep
    /// <c>OPTIONS</c> off the hot path, short enough that tightening the policy takes effect
    /// the same day.
    /// </summary>
    public TimeSpan PreflightMaxAge { get; init; } = TimeSpan.FromMinutes(10);
}
