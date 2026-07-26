using Api.Attributes;
using Api.Handlers.Authorization;
using Microsoft.AspNetCore.Authorization;

namespace Api.Extensions;

public static partial class ServiceCollectionExtensions
{
    /// <summary>
    /// Permission policies, step-up, and the deny-by-default fallback (§5).
    /// </summary>
    public static IServiceCollection AddAuthorizationServices(this IServiceCollection services)
    {
        ValidatePermissionCatalog();

        services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
        services.AddSingleton<IAuthorizationHandler, PermissionAuthorizationHandler>();
        services.AddSingleton<IAuthorizationHandler, RecentAuthAuthorizationHandler>();

        services.AddAuthorization(options =>
        {
            // ┌── ACTIVE as of §12 ────────────────────────────────────────────────────┐
            // │  Deny-by-default: any endpoint carrying no authorization metadata now   │
            // │  requires an authenticated user. A forgotten [Authorize] fails closed.  │
            // └────────────────────────────────────────────────────────────────────────┘
            //
            // §5 wrote and tested this policy but could not switch it on: the authorization
            // middleware applies the fallback to every request, and with no authentication
            // scheme registered there was nothing to challenge with, so every request —
            // 404s included — became a 500. §12's schemes are what made it activatable.
            //
            // Two consequences worth knowing, because both look like bugs otherwise:
            //   * A request matching NO endpoint is also subject to the fallback, so an
            //     unknown path answers 401 rather than 404. That is the intended reading —
            //     an anonymous caller learns nothing about which paths exist.
            //   * Anything that must stay anonymous needs to say so explicitly, including
            //     non-controller endpoints: the OpenAPI document is mapped with
            //     .AllowAnonymous() in the pipeline for this reason.
            options.FallbackPolicy = AuthorizationPolicies.DenyByDefault;

            options.AddPolicy(
                RequireRecentAuthAttribute.PolicyName,
                policy => policy
                    .RequireAuthenticatedUser()
                    .AddRequirements(new RecentAuthRequirement()));
        });

        return services;
    }

    /// <summary>
    /// Fails startup if the permission catalog and the role map have drifted apart.
    /// </summary>
    /// <remarks>
    /// Two failure modes, both silent at runtime and both caught here instead:
    /// a permission constant granted to nobody looks like a working authorization rule that
    /// denies everyone; a map entry naming a deleted constant is dead configuration that
    /// reads as if it still grants something.
    /// </remarks>
    private static void ValidatePermissionCatalog()
    {
        var declared = Permissions.All.ToHashSet(StringComparer.Ordinal);
        var mapped = RolePermissionMap.AllMappedPermissions();

        var unmapped = declared.Except(mapped, StringComparer.Ordinal).ToList();
        var unknown = mapped.Except(declared, StringComparer.Ordinal).ToList();

        if (unmapped.Count > 0 || unknown.Count > 0)
        {
            throw new InvalidOperationException(
                $"Permission catalog and role map disagree. " +
                $"Declared but granted to no role: [{string.Join(", ", unmapped)}]. " +
                $"Granted but not declared: [{string.Join(", ", unknown)}].");
        }
    }
}
