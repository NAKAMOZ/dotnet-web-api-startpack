namespace Api.Models;

/// <summary>
/// An authorization role. Two are seeded in §8: <c>Admin</c> and <c>User</c>.
/// </summary>
/// <remarks>
/// Roles are rows; <b>permissions are not</b>. Permissions are code constants mapped to
/// role names by <c>RolePermissionMap</c> and validated against the catalog at startup
/// (Authorization.md §3). Making them rows would move an authorization decision out of code
/// review and into whoever holds a database connection. DB-driven permissions are §29 work.
/// </remarks>
public sealed class Role : IAuditableEntity
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    /// <summary>
    /// Unique (§7). Must match a constant in <c>Roles</c> — the name is what lands in the
    /// <c>roles</c> claim and what the permission map is keyed by, so a row naming a role
    /// this build does not know grants nothing.
    /// </summary>
    public required string Name { get; set; }

    public string? Description { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public ICollection<UserRole> UserRoles { get; } = [];
}
