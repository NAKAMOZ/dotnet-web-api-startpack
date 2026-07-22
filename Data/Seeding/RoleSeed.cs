using Api.Handlers.Authorization;
using Api.Models;

namespace Api.Data.Seeding;

/// <summary>
/// The two roles the authorization model is built on (Authorization.md §2), seeded through
/// <c>HasData</c> so they are part of the migration rather than of runtime startup logic.
/// </summary>
/// <remarks>
/// Reference data that the application's behaviour depends on belongs in the schema
/// history: a database built from migrations alone is then complete, and every environment
/// gets identical rows without an extra deployment step.
/// <para>
/// <b>Everything here is deterministic, and it has to be.</b> <c>HasData</c> is diffed
/// against the model on every scaffold — a generated GUID or a <c>UtcNow</c> would make
/// each `migrations add` emit spurious updates, and worse, would give the same logical role
/// different ids in different environments.
/// </para>
/// </remarks>
public static class RoleSeed
{
    /// <summary>Fixed id for <c>Admin</c>. Referenced by the development seeder.</summary>
    public static readonly Guid AdminRoleId = new("0198f3a0-0000-7000-8000-000000000001");

    /// <summary>Fixed id for <c>User</c>. Referenced by the development seeder.</summary>
    public static readonly Guid UserRoleId = new("0198f3a0-0000-7000-8000-000000000002");

    /// <summary>
    /// The stamp on the seeded rows. A constant, not the current time: the
    /// <c>AuditableEntityInterceptor</c> never runs for <c>HasData</c> — those rows are
    /// written by the migration, not by <c>SaveChanges</c> — so the value has to be
    /// supplied, and it has to be stable across scaffolds.
    /// </summary>
    private static readonly DateTimeOffset SeededAt = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    /// <summary>The seed rows, in the order they are declared in <see cref="Roles"/>.</summary>
    public static IReadOnlyList<Role> All { get; } =
    [
        new()
        {
            Id = AdminRoleId,
            Name = Roles.Admin,
            Description = "Full administrative access — every permission in the catalog.",
            CreatedAt = SeededAt,
            UpdatedAt = SeededAt,
        },
        new()
        {
            Id = UserRoleId,
            Name = Roles.User,
            Description = "Ordinary account. Holds no cross-user permissions.",
            CreatedAt = SeededAt,
            UpdatedAt = SeededAt,
        },
    ];
}
