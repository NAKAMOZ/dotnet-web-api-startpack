using Api.Configuration;
using Api.Middleware;
using Serilog;

namespace Api.Extensions;

/// <summary>
/// Request-pipeline composition. Middleware order is security-relevant and is asserted
/// by tests in §22 — do not reorder without reading that suite first.
/// </summary>
public static partial class ApplicationBuilderExtensions
{
    /// <summary>
    /// Builds the HTTP request pipeline and maps the controller endpoints.
    /// </summary>
    /// <remarks>
    /// ── The order, and why each position is what it is (§14) ──────────────────────────
    /// <list type="number">
    /// <item>
    /// <b>Forwarded headers</b> (§27, production). Must be first: every stage below that
    /// reads a scheme or a client IP — HTTPS redirection, rate limiting, audit rows — reads
    /// the proxy's values otherwise.
    /// </item>
    /// <item>
    /// <b>Correlation id.</b> Before the exception handler, so a failure that happens
    /// anywhere below still carries an id a support conversation can start from.
    /// </item>
    /// <item>
    /// <b>Serilog request logging</b> (§15). After the correlation id, so the request log
    /// line carries it; before the exception handler, so it also records the requests that
    /// end in a 500.
    /// </item>
    /// <item>
    /// <b>Exception handling.</b> Everything below it is covered. Above it, nothing is —
    /// which is why the two stages above are the two that cannot throw meaningfully.
    /// </item>
    /// <item>
    /// <b>HSTS and HTTPS redirection.</b> Below the exception handler because a redirect is
    /// a normal response, not an error path.
    /// </item>
    /// <item>
    /// <b>Security headers.</b> Registered as response-starting callbacks, so the headers
    /// survive the exception handler clearing the response — an error body is served with
    /// the same policy as a success body.
    /// </item>
    /// <item>
    /// <b>Rate limiting</b> (§17). Before authentication, so an unauthenticated flood is
    /// throttled before it costs a database read or an Argon2 verification. After it, every
    /// rejected request has already paid for the work.
    /// </item>
    /// <item>
    /// <b>CORS.</b> Before authentication: a preflight <c>OPTIONS</c> carries no credentials
    /// by design, and behind deny-by-default authentication it would answer 401 — which a
    /// browser reads as "not allowed" and reports as a CORS failure with no hint of the
    /// cause.
    /// </item>
    /// <item>
    /// <b>Authentication, then authorization.</b> Identity is established before it is
    /// judged. Reversed, every check runs against an anonymous principal.
    /// </item>
    /// <item>
    /// <b>Endpoints.</b> The CSRF filter runs here as an MVC authorization filter — after
    /// authentication, because it has to know which scheme the request used before it can
    /// decide whether the request is exempt.
    /// </item>
    /// </list>
    /// </remarks>
    public static WebApplication UseApiPipeline(this WebApplication app)
    {
        // TODO §27: ForwardedHeadersMiddleware, in production only and with a known-proxy
        // allowlist. Accepting X-Forwarded-For from anywhere lets any caller choose the IP
        // that lands in the rate limiter and the audit trail.

        // ── 2. Correlation id ────────────────────────────────────────────────────────
        // Adopts a validated inbound X-Correlation-Id or mints one, publishes it on
        // HttpContext.Items, and echoes it on the response.
        app.UseMiddleware<CorrelationIdMiddleware>();

        // ── 3. Request logging ───────────────────────────────────────────────────────
        // One structured summary event per request, replacing the framework's several lines
        // of unstructured per-request noise (ADR-0010). Above the exception handler, so it
        // also records the requests that end in a 500 — and it sees the final status code,
        // because the handler runs inside it.
        //
        // The correlation id is not passed here: CorrelationIdEnricher attaches it to every
        // event, this one included.
        app.UseSerilogRequestLogging();

        // ── 4. Exception handling ────────────────────────────────────────────────────
        // Dispatches to ExceptionHandlingMiddleware (an IExceptionHandler), which maps
        // through §13's single table and writes the RFC 9457 body.
        app.UseExceptionHandler();

        // Gives a body to status codes that would otherwise have none (§13).
        //
        // An authorization challenge is not an "error result" — the middleware sets 401 and
        // returns, so nothing ever runs the Problem Details writer and the client receives a
        // bare status with an empty body. Same for a 404 from routing and a 405 from a
        // method mismatch. With this in place every one of them is RFC 9457, which is what
        // makes "one envelope for every non-2xx" true rather than aspirational.
        app.UseStatusCodePages();

        if (app.Environment.IsDevelopment())
        {
            // OpenAPI document is exposed in Development only; Scalar UI arrives in §18
            // and is disabled in production (P16, still pending).
            //
            // AllowAnonymous is required as of §12: the deny-by-default fallback applies to
            // every endpoint without authorization metadata, and this one has none — without
            // the opt-out the document itself answers 401 in development.
            app.MapOpenApi().AllowAnonymous();
        }
        else
        {
            // ── 5. HSTS ──────────────────────────────────────────────────────────────
            // Development only ever runs on localhost, where a browser that has cached an
            // HSTS entry for `localhost` will refuse plain HTTP for every other project on
            // the machine — including ones that have no HTTPS profile at all.
            app.UseHsts();
        }

        app.UseHttpsRedirection();

        // ── 6. Security headers ──────────────────────────────────────────────────────
        app.UseMiddleware<SecurityHeadersMiddleware>();

        // TODO §17: rate limiting, here — before authentication, so unauthenticated abuse is
        // throttled before it reaches a password hash.

        // ── 8. CORS ──────────────────────────────────────────────────────────────────
        // The policy comes from OriginAwareCorsPolicyProvider, which offers credentials to
        // cookie-mode origins only. The policy name is passed for clarity; the provider
        // answers regardless of it.
        app.UseCors(ApiCorsOptions.PolicyName);

        // ── 9. Authentication → authorization ────────────────────────────────────────
        //
        // Order is not negotiable — authentication establishes who the caller is,
        // authorization decides what they may do with that identity. Reversed, every check
        // runs against an anonymous principal and denies everything.
        //
        // As of §12 these run against the real schemes: a policy scheme that forwards to
        // JwtBearer (bearer header or access cookie) or to ApiKey, and the deny-by-default
        // fallback is active behind them.
        app.UseAuthentication();
        app.UseAuthorization();

        // ── 10. Endpoints ────────────────────────────────────────────────────────────
        app.MapControllers();

        return app;
    }
}
