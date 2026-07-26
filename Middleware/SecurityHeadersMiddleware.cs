namespace Api.Middleware;

/// <summary>
/// Writes the response security headers that apply to every response this API produces (§14).
/// </summary>
/// <remarks>
/// The policy is written for an API, not for a site: this origin serves JSON, so it declares
/// that it loads nothing, frames nothing and is framed by nobody. That is a far stronger
/// statement than a page-oriented CSP can make, and it costs nothing here.
/// <para>
/// The headers matter even though a JSON response is not a document. Two paths make them
/// load-bearing: an error or an upload echo can be sniffed into HTML by an older browser
/// (<c>nosniff</c> closes that), and any endpoint that ever returns a rendered body — the
/// documentation UI in §18 — inherits the same origin.
/// </para>
/// </remarks>
public sealed class SecurityHeadersMiddleware(RequestDelegate next)
{
    /// <summary>
    /// Path prefix the API documentation UI is mounted under (§18).
    /// </summary>
    /// <remarks>
    /// The one place the strict policy is relaxed, because Scalar is a real page with real
    /// scripts and styles. Kept as a prefix constant so the relaxation is greppable and
    /// cannot spread by accident.
    /// </remarks>
    public const string DocumentationPathPrefix = "/scalar";

    /// <summary>Path prefix for the self-hosted development endpoint workbench.</summary>
    public const string PlaygroundPathPrefix = "/playground";

    /// <summary>
    /// The API policy: this origin loads nothing at all.
    /// </summary>
    /// <remarks>
    /// <c>default-src 'none'</c> covers every fetch directive, so scripts, styles, images,
    /// frames and connections are all denied without listing them. The three that are not
    /// covered by <c>default-src</c> are spelled out: <c>frame-ancestors</c> (clickjacking),
    /// <c>base-uri</c> (a base tag rewriting relative URLs) and <c>form-action</c> (a
    /// posted form leaving for an attacker's host).
    /// </remarks>
    private const string ApiContentSecurityPolicy =
        "default-src 'none'; frame-ancestors 'none'; base-uri 'none'; form-action 'none'";

    /// <summary>
    /// The documentation policy — same-origin only, with inline script and style allowed.
    /// </summary>
    /// <remarks>
    /// <b>§18 must self-host the Scalar assets.</b> This policy has no CDN host in it, so a
    /// default Scalar configuration that pulls its bundle from jsdelivr renders a blank page.
    /// That is the intended failure: adding a third-party script host to a CSP is a decision
    /// that belongs in §18 with an ADR, not something this middleware should pre-approve.
    /// </remarks>
    private const string DocumentationContentSecurityPolicy =
        "default-src 'self'; script-src 'self' 'unsafe-inline'; style-src 'self' 'unsafe-inline'; " +
        "img-src 'self' data:; font-src 'self' data:; connect-src 'self'; " +
        "frame-ancestors 'none'; base-uri 'none'; form-action 'self'";

    /// <summary>
    /// Browser features this origin will never use, denied for itself and every frame.
    /// </summary>
    /// <remarks>
    /// An empty allowlist <c>()</c> means "nobody, including this document". The list is
    /// short on purpose — it names the capabilities with a privacy or credential impact
    /// rather than enumerating the whole registry, which changes with every browser release
    /// and would rot into noise.
    /// </remarks>
    private const string PermissionsPolicy =
        "accelerometer=(), autoplay=(), camera=(), display-capture=(), encrypted-media=(), " +
        "fullscreen=(), geolocation=(), gyroscope=(), magnetometer=(), microphone=(), " +
        "midi=(), payment=(), usb=(), xr-spatial-tracking=()";

    public Task InvokeAsync(HttpContext context)
    {
        // Registered as a starting callback for the same reason the correlation id is: the
        // exception handler clears the response before writing a problem body, and headers
        // written on the way in would be lost on precisely the responses an attacker is
        // most likely to be looking at.
        context.Response.OnStarting(static state =>
        {
            var http = (HttpContext)state;
            var headers = http.Response.Headers;

            // Content-type sniffing is what turns a JSON error echoing user input into
            // executable HTML in the browser's eyes. One header closes it.
            headers.XContentTypeOptions = "nosniff";

            // No Referer header leaves this origin, ever. API URLs carry ids, and some
            // carry one-time tokens in the query string (email verification links land on
            // the client, but the redirect chain is not ours to assume).
            headers["Referrer-Policy"] = "no-referrer";

            headers["Permissions-Policy"] = PermissionsPolicy;

            // frame-ancestors in the CSP is the modern control; X-Frame-Options is kept for
            // clients that do not implement it. They agree, so there is no conflict to
            // resolve — this is redundancy, not policy duplication.
            headers.XFrameOptions = "DENY";

            // Blocks another origin from embedding this one's responses as a subresource,
            // which is the remaining read path Spectre-class attacks use.
            headers["Cross-Origin-Resource-Policy"] = "same-origin";

            headers.ContentSecurityPolicy = IsInteractiveSiteRequest(http.Request)
                ? DocumentationContentSecurityPolicy
                : ApiContentSecurityPolicy;

            // Nothing this API returns is cacheable by an intermediary: every authenticated
            // response is caller-specific, and several carry show-once secrets. Set here
            // rather than per action so a new endpoint cannot forget it.
            headers.CacheControl = "no-store";

            return Task.CompletedTask;
        }, context);

        return next(context);
    }

    private static bool IsInteractiveSiteRequest(HttpRequest request) =>
        request.Path.StartsWithSegments(DocumentationPathPrefix, StringComparison.OrdinalIgnoreCase)
        || request.Path.StartsWithSegments(PlaygroundPathPrefix, StringComparison.OrdinalIgnoreCase);
}
