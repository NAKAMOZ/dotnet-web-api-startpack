using Microsoft.AspNetCore.Authorization;

namespace Api.Attributes;

/// <summary>
/// Requires a permission from <see cref="Handlers.Authorization.Permissions"/>.
/// </summary>
/// <remarks>
/// Encodes the permission into a policy name that
/// <c>PermissionPolicyProvider</c> parses back out, so no policy has to be registered per
/// permission. Always pass a constant — a literal here is a magic string that the map
/// cannot be checked against.
/// <code>
/// [RequirePermission(Permissions.UsersReadAny)]
/// public Task&lt;ActionResult&gt; ListUsers() { }
/// </code>
/// </remarks>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public sealed class RequirePermissionAttribute : AuthorizeAttribute
{
    /// <summary>Policy-name prefix identifying a dynamically generated permission policy.</summary>
    public const string PolicyPrefix = "perm:";

    /// <param name="permission">A constant from <see cref="Handlers.Authorization.Permissions"/>.</param>
    public RequirePermissionAttribute(string permission)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(permission);
        Permission = permission;
        Policy = PolicyPrefix + permission;
    }

    /// <summary>The required permission.</summary>
    public string Permission { get; }
}
