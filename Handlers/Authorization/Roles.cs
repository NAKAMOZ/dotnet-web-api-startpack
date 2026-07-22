namespace Api.Handlers.Authorization;

/// <summary>
/// The two seeded role names. Constants rather than literals so a typo is a compile error
/// instead of a silently-never-matching policy.
/// </summary>
public static class Roles
{
    /// <summary>Full administrative access. Seeded in §8.</summary>
    public const string Admin = "Admin";

    /// <summary>Ordinary account. Holds no cross-user permissions — see <see cref="RolePermissionMap"/>.</summary>
    public const string User = "User";

    /// <summary>Every role known to the system.</summary>
    public static IReadOnlyList<string> All { get; } = [Admin, User];
}
