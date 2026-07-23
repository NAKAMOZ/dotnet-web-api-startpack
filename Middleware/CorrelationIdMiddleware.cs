namespace Api.Middleware;

/// <summary>
/// Resolves one correlation id per request — a validated inbound header or a fresh value —
/// publishes it on <c>HttpContext.Items</c> and echoes it on the response (§14).
/// </summary>
/// <remarks>
/// First in the pipeline after forwarded headers, and deliberately outside the exception
/// handler: an error response is the response that most needs an id, so the id has to exist
/// before anything can fail.
/// <para>
/// A caller-supplied id is <b>adopted, never trusted</b>. It is validated against
/// <see cref="CorrelationId.IsWellFormed"/> and silently replaced when it does not pass —
/// rejecting with a 400 would turn the header into a probe that tells an attacker exactly
/// what the server parses.
/// </para>
/// </remarks>
public sealed class CorrelationIdMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var inbound = context.Request.Headers[CorrelationId.HeaderName].ToString();

        var correlationId = CorrelationId.IsWellFormed(inbound)
            ? inbound
            : CorrelationId.New();

        context.Items[CorrelationId.ItemsKey] = correlationId;

        // ── Why OnStarting rather than a plain header write ──────────────────────────
        //
        // UseExceptionHandler clears the response — status, headers and body — before it
        // writes the Problem Details body. A header set here on the way in would therefore
        // survive on every 2xx and disappear on exactly the 5xx responses a support
        // conversation starts from. Callbacks registered with OnStarting are held by the
        // response feature, are not cleared with it, and run once immediately before the
        // first byte goes out — after any handler has finished rewriting the response.
        context.Response.OnStarting(static state =>
        {
            var http = (HttpContext)state;

            http.Response.Headers[CorrelationId.HeaderName] =
                (string?)http.Items[CorrelationId.ItemsKey];

            return Task.CompletedTask;
        }, context);

        // §15 resolved the LogContext TODO that stood here differently: the id reaches log
        // events through Api.Logging.CorrelationIdEnricher, which reads this same Items
        // entry when an event is written. A push here would work, but its sibling
        // UserIdEnricher cannot be a push — the user id does not exist this early, five
        // stages above authentication — and one pushed property beside one enriched property
        // reads like a bug. This file also stays free of a Serilog reference.
        await next(context);
    }
}
