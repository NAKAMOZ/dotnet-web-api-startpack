using Api.Middleware;
using Serilog.Core;
using Serilog.Events;

namespace Api.Logging;

/// <summary>
/// Attaches the request's correlation id to every log event written during it (§15).
/// </summary>
/// <remarks>
/// <b>An enricher rather than a <c>LogContext.PushProperty</c> in the middleware.</b> §14
/// left a TODO for the push, and the push is the more obvious shape — but it fixes the
/// property's value at the moment the scope opens, and <see cref="UserIdEnricher"/> needs
/// the opposite: the user id is not known where the correlation middleware runs, because
/// authentication is five stages further down the pipeline. One of the two enrichers had to
/// read <see cref="HttpContext"/> at emit time, and having the pair behave differently is
/// how the next reader concludes one of them is broken. Reading at emit time also keeps
/// <c>Middleware/</c> free of a Serilog reference.
/// </remarks>
public sealed class CorrelationIdEnricher(IHttpContextAccessor httpContextAccessor) : ILogEventEnricher
{
    /// <summary>
    /// Property name on the log event. Matches the header and the Problem Details extension
    /// so one id is searched for under one name in all three places.
    /// </summary>
    public const string PropertyName = "CorrelationId";

    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        // No HttpContext means a startup or background-worker event (§12's cleanup job).
        // Those have no correlation id and must not invent one — an id that matches no
        // request is worse than an absent one.
        if (httpContextAccessor.HttpContext?.Items[CorrelationId.ItemsKey] is not string correlationId)
        {
            return;
        }

        // AddPropertyIfAbsent, not AddOrUpdateProperty: a call site that logged an explicit
        // CorrelationId — replaying another request's id during an investigation, say —
        // meant it, and the ambient value must not overwrite it.
        logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty(PropertyName, correlationId));
    }
}
