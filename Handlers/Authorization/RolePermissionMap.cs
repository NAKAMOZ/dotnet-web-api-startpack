namespace Api.Handlers.Authorization;

/// <summary>
/// Static role → permission assignment. The whole authorization model in one readable
/// place, which is the point of keeping it in code rather than in the database for v1.
/// </summary>
public static class RolePermissionMap
{
    private static readonly Dictionary<string, IReadOnlySet<string>> Map = new(StringComparer.Ordinal)
    {
        [Roles.Admin] = new HashSet<string>(Permissions.All, StringComparer.Ordinal),

        // Deliberately empty. An ordinary user needs no permission to reach their own
        // profile, sessions, passkeys or API keys — those routes resolve the subject from
        // the `sub` claim and never accept a user id, so there is nothing to authorize
        // beyond being authenticated. Granting `User` a permission here would mean it
        // applies across all users, which is exactly what must not happen.
        [Roles.User] = new HashSet<string>(StringComparer.Ordinal),
    };

    /// <summary>
    /// Whether any of the caller's roles grants the permission.
    /// </summary>
    /// <param name="roles">Role names from the token's <c>roles</c> claim.</param>
    /// <param name="permission">A constant from <see cref="Permissions"/>.</param>
    /// <returns><see langword="true"/> if at least one role grants it.</returns>
    public static bool Grants(IEnumerable<string> roles, string permission)
    {
        foreach (var role in roles)
        {
            if (Map.TryGetValue(role, out var granted) && granted.Contains(permission))
            {
                return true;
            }
        }

        // Unknown roles grant nothing. A role present in a token but absent from this map
        // is not an error to swallow silently — §15 logs it, because it means a token was
        // issued against a role this build does not know about.
        return false;
    }

    /// <summary>Permissions granted to a role, or an empty set if the role is unknown.</summary>
    public static IReadOnlySet<string> PermissionsFor(string role) =>
        Map.TryGetValue(role, out var granted) ? granted : new HashSet<string>(StringComparer.Ordinal);

    /// <summary>
    /// Every permission referenced by the map. Compared against <see cref="Permissions.All"/>
    /// at startup so a permission that exists as a constant but is granted to nobody, or a
    /// map entry naming a permission that no longer exists, fails fast.
    /// </summary>
    public static IReadOnlySet<string> AllMappedPermissions() =>
        Map.Values.SelectMany(static p => p).ToHashSet(StringComparer.Ordinal);
}
