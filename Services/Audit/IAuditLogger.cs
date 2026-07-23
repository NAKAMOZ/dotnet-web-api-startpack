using Api.Models.Enums;

namespace Api.Services.Audit;

/// <summary>
/// Writes the security audit trail (§15).
/// </summary>
/// <remarks>
/// <b>Not a logger in the Serilog sense, despite the name.</b> Operational logs are
/// diagnostic, sampled and disposable; an audit row is a durable security record with its own
/// retention period (90 days, P18) and its own query endpoint. The two systems answer
/// different questions for different audiences and are deliberately not one system
/// (ADR-0010).
/// <para>
/// <b>Write-only by design.</b> Reading the trail is <see cref="IAuditQueryService"/>, behind
/// the <c>audit:read</c> permission. Splitting them keeps the interface that §12's services
/// depend on incapable of reading anything — a service that records a login has no reason to
/// be able to enumerate everyone else's.
/// </para>
/// <para>
/// Request context — ip address, user agent, correlation id — is resolved by the
/// implementation, not passed by the caller. §12's services take no <c>HttpContext</c>, and
/// giving them one so they could fill in an audit row would be the wrong trade.
/// </para>
/// </remarks>
public interface IAuditLogger
{
    /// <summary>
    /// Records one audit event.
    /// </summary>
    /// <param name="eventType">The catalog member. Adding a new kind of event means adding it to <see cref="AuditEventType"/> first.</param>
    /// <param name="userId">
    /// Subject of the event. Null falls back to the authenticated caller, and stays null when
    /// there is none — a failed login against an address that does not exist has no subject,
    /// and inventing one would be a lie in the one table kept for not lying.
    /// </param>
    /// <param name="metadata">
    /// Event-specific detail, serialized to the <c>jsonb</c> column. Passed through
    /// <see cref="AuditMetadataSerializer"/>, which redacts credential-shaped fields — but
    /// the redaction is a backstop, not a licence to hand it a whole request object.
    /// </param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task LogAsync(
        AuditEventType eventType,
        Guid? userId = null,
        object? metadata = null,
        CancellationToken cancellationToken = default);
}
