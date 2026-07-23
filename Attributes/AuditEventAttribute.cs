using Api.Models.Enums;

namespace Api.Attributes;

/// <summary>
/// Marks an action whose successful execution is a security event, and names the catalog
/// member it is recorded as (§15).
/// </summary>
/// <remarks>
/// Read by <c>AuditActionFilter</c>, which is registered globally and does nothing to an
/// action that does not carry this attribute.
/// <para>
/// <b>Why the mapping lives on the action rather than in the filter.</b> A table inside the
/// filter mapping controller and action names to event types is a second place that has to be
/// edited when a controller is renamed, and a rename does not break it — it silently stops
/// matching, and the events stop being recorded. Here the mapping moves with the code it
/// describes.
/// </para>
/// <para>
/// Actions whose event is recorded by the service that performs the work — every login,
/// refresh and revocation path in §12 — must <b>not</b> carry this attribute as well, or the
/// event lands in the trail twice.
/// </para>
/// </remarks>
[AttributeUsage(AttributeTargets.Method)]
public sealed class AuditEventAttribute(AuditEventType eventType) : Attribute
{
    public AuditEventType EventType { get; } = eventType;
}
