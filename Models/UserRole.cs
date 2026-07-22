namespace Api.Models;

/// <summary>
/// Join row between <see cref="User"/> and <see cref="Role"/>. Composite key
/// (<see cref="UserId"/>, <see cref="RoleId"/>) — the database, not the service, is what
/// makes a duplicate assignment impossible.
/// </summary>
/// <remarks>
/// An explicit entity rather than an EF-managed implicit join table: role grants are
/// audited events, and <see cref="IAuditableEntity.CreatedAt"/> answering "when was this
/// user made an admin?" is worth the extra class.
/// </remarks>
public sealed class UserRole : IAuditableEntity
{
    public Guid UserId { get; set; }

    public User User { get; set; } = null!;

    public Guid RoleId { get; set; }

    public Role Role { get; set; } = null!;

    /// <summary>When the role was granted.</summary>
    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
