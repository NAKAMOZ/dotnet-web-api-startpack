using System.Security.Claims;
using Api.Data;
using Api.Middleware;
using Api.Models;
using Api.Models.Enums;

namespace Api.Services.Audit;

/// <summary>
/// Writes audit rows on their own connection, independently of whatever transaction the
/// calling service is in (§15).
/// </summary>
/// <remarks>
/// <b>Why a separate scope rather than the request's <see cref="AppDbContext"/>.</b> Two
/// reasons, and the second is the one that matters.
/// <para>
/// First, calling <c>SaveChangesAsync</c> on the request's context would flush whatever else
/// that context happens to be tracking — an audit call placed mid-operation would commit half
/// a registration.
/// </para>
/// <para>
/// Second, and the actual design point: <b>the events most worth recording are the ones whose
/// transaction is about to roll back.</b> <c>login_failed</c> has nothing else to commit.
/// <c>token_reuse_detected</c> fires on the path that revokes a session and may itself throw.
/// An audit row enlisted in the caller's transaction disappears exactly when an incident
/// happens, which is the opposite of what the trail is for.
/// </para>
/// <para>
/// The cost is a second connection per audit event and no atomicity with the operation being
/// recorded — so a row can exist for an operation that then failed. For an audit trail that is
/// the right direction: "this was attempted" is the claim being made, not "this succeeded".
/// </para>
/// </remarks>
public sealed class AuditLogger(
    IServiceScopeFactory serviceScopeFactory,
    IHttpContextAccessor httpContextAccessor,
    TimeProvider timeProvider,
    ILogger<AuditLogger> logger) : IAuditLogger
{
    /// <summary>Matches the column length in <c>AuditLogEntryConfiguration</c>.</summary>
    private const int MaxUserAgentLength = 512;

    /// <summary>Enough for an IPv6 address with an IPv4 tail; the column agrees.</summary>
    private const int MaxIpAddressLength = 45;

    public async Task LogAsync(
        AuditEventType eventType,
        Guid? userId = null,
        object? metadata = null,
        CancellationToken cancellationToken = default)
    {
        var httpContext = httpContextAccessor.HttpContext;

        var entry = new AuditLogEntry
        {
            // The explicit subject wins. Falling back to the caller matters for self-service
            // events; for admin actions the two differ, and the row records the target while
            // the metadata records the actor.
            UserId = userId ?? CurrentUserId(httpContext),
            EventType = eventType,
            IpAddress = Truncate(httpContext?.Connection.RemoteIpAddress?.ToString(), MaxIpAddressLength),
            UserAgent = Truncate(httpContext?.Request.Headers.UserAgent.ToString(), MaxUserAgentLength),
            CorrelationId = httpContext?.Items[CorrelationId.ItemsKey] as string,
            Metadata = AuditMetadataSerializer.Serialize(metadata),
            OccurredAt = timeProvider.GetUtcNow(),
        };

        try
        {
            // A scope of its own: a fresh AppDbContext, a fresh connection, and no knowledge
            // of what the request's context is tracking.
            await using var scope = serviceScopeFactory.CreateAsyncScope();

            var database = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            database.AuditLogEntries.Add(entry);

            await database.SaveChangesAsync(cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            HandleWriteFailure(entry, exception);
        }
    }

    /// <summary>
    /// What happens when the audit row cannot be written — the database is unreachable, the
    /// insert times out, the disk is full.
    /// </summary>
    /// <remarks>
    /// TODO: decision pending — see the note accompanying this change. The two defensible
    /// answers are "record the loss and let the request succeed" and "fail the request,
    /// because an unrecorded security event is worse than a failed one", and they are not
    /// reconcilable. <paramref name="entry"/> carries everything the fallback would need.
    /// </remarks>
    private void HandleWriteFailure(AuditLogEntry entry, Exception exception)
    {
        // TODO §15: implement the chosen policy.
        _ = logger;
        _ = entry;
        _ = exception;
    }

    private static Guid? CurrentUserId(HttpContext? httpContext)
    {
        var user = httpContext?.User;

        if (user?.Identity?.IsAuthenticated is not true)
        {
            return null;
        }

        // Both names, in the order ApiControllerBase reads them: the JWT handler maps `sub`
        // onto ClaimTypes.NameIdentifier, the API-key handler issues the short name directly.
        var claim = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? user.FindFirstValue("sub");

        return Guid.TryParse(claim, out var parsed) ? parsed : null;
    }

    /// <summary>
    /// Truncates rather than lets the insert fail. A 4 KB user-agent string is a client quirk
    /// or a probe; either way, losing the audit row over it would be the worse outcome.
    /// </summary>
    private static string? Truncate(string? value, int maxLength) =>
        string.IsNullOrEmpty(value)
            ? null
            : value.Length <= maxLength ? value : value[..maxLength];
}
