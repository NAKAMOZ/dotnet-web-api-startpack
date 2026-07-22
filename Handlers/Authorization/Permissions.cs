namespace Api.Handlers.Authorization;

/// <summary>
/// The permission catalog. Permissions are **code constants**, not database rows — v1 keeps
/// the schema lean and defers runtime-editable permissions to §29.
/// </summary>
/// <remarks>
/// Naming is <c>resource:action[:scope]</c>. The <c>:any</c> suffix means "across all
/// users" and is the part that makes a permission administrative — self-service access to
/// one's own resources carries no permission at all, because those routes resolve the
/// subject from the <c>sub</c> claim and never take a user id (see
/// <c>Documentation/Architecture/Authorization.md</c>).
/// </remarks>
public static class Permissions
{
    /// <summary>Read any user's profile. <c>GET /admin/users</c>, <c>GET /admin/users/{userId}</c>.</summary>
    public const string UsersReadAny = "users:read:any";

    /// <summary>Modify any user. <c>PATCH /admin/users/{userId}</c>, including admin unlock.</summary>
    public const string UsersWriteAny = "users:write:any";

    /// <summary>Delete any user. <c>DELETE /admin/users/{userId}</c>.</summary>
    public const string UsersDeleteAny = "users:delete:any";

    /// <summary>Grant a role. <c>POST /admin/users/{userId}/roles</c>.</summary>
    public const string RolesAssign = "roles:assign";

    /// <summary>Revoke a role. <c>DELETE /admin/users/{userId}/roles/{roleId}</c>.</summary>
    public const string RolesRevoke = "roles:revoke";

    /// <summary>Revoke another user's sessions. <c>DELETE /admin/users/{userId}/sessions</c>.</summary>
    public const string SessionsRevokeAny = "sessions:revoke:any";

    /// <summary>Read the security audit trail. <c>GET /admin/audit-logs</c>.</summary>
    public const string AuditRead = "audit:read";

    /// <summary>
    /// Every permission the system defines. Used to validate the role map at startup and by
    /// §22's coverage assertions.
    /// </summary>
    public static IReadOnlyList<string> All { get; } =
    [
        UsersReadAny,
        UsersWriteAny,
        UsersDeleteAny,
        RolesAssign,
        RolesRevoke,
        SessionsRevokeAny,
        AuditRead,
    ];
}
