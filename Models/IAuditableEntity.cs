namespace Api.Models;

/// <summary>
/// Marks an entity whose creation and last-modification times are stamped automatically by
/// <c>AuditableEntityInterceptor</c> (§7) from the injected <see cref="TimeProvider"/>.
/// </summary>
/// <remarks>
/// An interface rather than a base class deliberately: inheritance would force the two
/// columns onto entities that do not want them. <c>AuditLogEntry</c> is the concrete
/// counter-example — an audit row is append-only, so an <c>UpdatedAt</c> on it would be a
/// column whose only possible meaning is that something tampered with the trail.
/// <para>
/// Setters are public because the interceptor writes them. Application code does not:
/// assigning <c>CreatedAt</c> by hand in a service is how the two values drift apart.
/// </para>
/// </remarks>
public interface IAuditableEntity
{
    /// <summary>Set once, when the row is first inserted.</summary>
    DateTimeOffset CreatedAt { get; set; }

    /// <summary>Rewritten on every update. Equal to <see cref="CreatedAt"/> until the first change.</summary>
    DateTimeOffset UpdatedAt { get; set; }
}
