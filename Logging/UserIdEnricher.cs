using System.Security.Claims;
using Serilog.Core;
using Serilog.Events;

namespace Api.Logging;

/// <summary>
/// Attaches the authenticated caller's user id to log events (§15).
/// </summary>
/// <remarks>
/// The id only, never the email — an email is personal data with its own retention rules,
/// and the id answers the same operational question ("what else did this account do?")
/// without putting an address into every sink the logs are shipped to. The audit trail
/// stores the same id, so the two join.
/// <para>
/// Reads the claim at emit time rather than at a fixed point in the pipeline: events written
/// before authentication runs simply carry no user id, which is accurate. A scope opened
/// early would have to guess, and the guess is always "anonymous".
/// </para>
/// </remarks>
public sealed class UserIdEnricher(IHttpContextAccessor httpContextAccessor) : ILogEventEnricher
{
    public const string PropertyName = "UserId";

    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        var user = httpContextAccessor.HttpContext?.User;

        if (user?.Identity?.IsAuthenticated is not true)
        {
            return;
        }

        // Both names, in the order ApiControllerBase reads them: the JWT handler maps `sub`
        // to ClaimTypes.NameIdentifier, and the API-key handler issues the short name
        // directly. Checking one would silently lose every API-key request.
        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? user.FindFirstValue("sub");

        if (string.IsNullOrEmpty(userId))
        {
            return;
        }

        logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty(PropertyName, userId));
    }
}
