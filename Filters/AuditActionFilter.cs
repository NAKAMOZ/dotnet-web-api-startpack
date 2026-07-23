using System.Security.Claims;
using Api.Attributes;
using Api.Services.Audit;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Infrastructure;

namespace Api.Filters;

/// <summary>
/// Records an audit row for actions marked with <see cref="AuditEventAttribute"/>, after they
/// succeed (§14's deferred filter, §15's <see cref="IAuditLogger"/>).
/// </summary>
/// <remarks>
/// <b>Global, opt-in by attribute.</b> The roadmap places this filter "on admin controllers".
/// Registered globally instead, for the reason the CSRF filter is: a filter applied per
/// controller is one a new controller forgets, and the omission is invisible — an action that
/// is simply never audited looks exactly like an action nobody has performed. The attribute
/// then decides both whether and as what, so the blast radius of "global" is nil for the
/// actions that do not carry it.
/// <para>
/// <b>After the action, and only on success.</b> The trail records what happened, not what was
/// attempted and rejected — a 403 from the permission handler or a 400 from validation is the
/// system working, and those live in the request log. Failed <i>authentication</i> is the
/// exception, and it is recorded by the service that performs it (<c>login_failed</c>), not
/// here: this filter never runs for a request that authorization stopped.
/// </para>
/// </remarks>
public sealed class AuditActionFilter(IAuditLogger auditLogger) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var auditEvent = context.ActionDescriptor.EndpointMetadata
            .OfType<AuditEventAttribute>()
            .FirstOrDefault();

        if (auditEvent is null)
        {
            await next();
            return;
        }

        var executed = await next();

        if (!Succeeded(executed))
        {
            return;
        }

        // The row's subject is the target of the action, not the administrator performing it
        // — "what happened to this account" is the question the trail is queried with, and
        // AuditLogQuery.UserId filters on exactly this column. The actor goes in the metadata,
        // where an admin action is distinguishable from the user's own.
        await auditLogger.LogAsync(
            auditEvent.EventType,
            userId: TargetUserId(context),
            metadata: new
            {
                ActorUserId = ActorUserId(context),
                Action = context.ActionDescriptor.DisplayName,
                Method = context.HttpContext.Request.Method,
                Route = RouteValues(context),
            },
            cancellationToken: context.HttpContext.RequestAborted);
    }

    /// <summary>
    /// Whether the action produced a 2xx.
    /// </summary>
    /// <remarks>
    /// An unhandled exception counts as failure even though the exception handler will turn it
    /// into a response: the operation did not happen. A result with no status code of its own
    /// — an action that wrote to the response directly — is treated as 200, which is what MVC
    /// itself defaults to.
    /// <para>
    /// This is also what keeps §11's <c>501</c> placeholders out of the trail: they are
    /// <c>ObjectResult</c>s carrying 501, so nothing is recorded until the service behind the
    /// action is real.
    /// </para>
    /// </remarks>
    private static bool Succeeded(ActionExecutedContext executed)
    {
        if (executed.Exception is not null && !executed.ExceptionHandled)
        {
            return false;
        }

        var statusCode = (executed.Result as IStatusCodeActionResult)?.StatusCode
                         ?? StatusCodes.Status200OK;

        return statusCode is >= StatusCodes.Status200OK and < StatusCodes.Status300MultipleChoices;
    }

    /// <summary>
    /// The account the action acted on, from the route. Null when the route names none — the
    /// audit logger then falls back to the caller, which is the right subject for a
    /// self-service action.
    /// </summary>
    private static Guid? TargetUserId(ActionExecutingContext context) =>
        context.RouteData.Values.TryGetValue("userId", out var value)
        && Guid.TryParse(value?.ToString(), out var userId)
            ? userId
            : null;

    private static Guid? ActorUserId(ActionExecutingContext context)
    {
        var user = context.HttpContext.User;

        var claim = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? user.FindFirstValue("sub");

        return Guid.TryParse(claim, out var actorUserId) ? actorUserId : null;
    }

    /// <summary>
    /// Route values as recorded detail — which role was granted, which session was revoked.
    /// </summary>
    /// <remarks>
    /// Route values only, never the request body: a body may hold a password or a token, and
    /// serializing whichever one happens to arrive is how a credential reaches durable storage.
    /// <c>AuditMetadataSerializer</c> redacts by field name as a backstop; not passing the body
    /// is the actual control.
    /// </remarks>
    private static Dictionary<string, string?> RouteValues(ActionExecutingContext context) =>
        context.RouteData.Values.ToDictionary(
            value => value.Key,
            value => value.Value?.ToString(),
            StringComparer.Ordinal);
}
