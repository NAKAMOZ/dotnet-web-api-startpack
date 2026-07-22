using Api.Attributes;
using Api.Configuration;
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

        // Step-up reads its window from configuration. Bound here rather than left to
        // defaults so a value set in appsettings actually takes effect — an unbound options
        // class silently serves its defaults and gives no sign that configuration was
        // ignored. §25 extends this to every options class with full startup validation.
        services
            .AddOptions<AuthSessionOptions>()
            .BindConfiguration(AuthSessionOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
        services.AddSingleton<IAuthorizationHandler, PermissionAuthorizationHandler>();
        services.AddSingleton<IAuthorizationHandler, RecentAuthAuthorizationHandler>();

        services.AddAuthorization(options =>
        {
            // ┌── TODO §12 — ONE LINE, DO NOT SHIP v1 WITHOUT IT ──────────────────────┐
            // │  options.FallbackPolicy = AuthorizationPolicies.DenyByDefault;         │
            // └────────────────────────────────────────────────────────────────────────┘
            //
            // Deny-by-default is §5's core guarantee, and it is deliberately NOT active
            // yet. Calling AddAuthorization makes minimal hosting insert the authorization
            // middleware automatically, and that middleware applies the fallback policy to
            // every request — including ones matching no endpoint. With no authentication
            // scheme registered there is nothing to challenge with, so setting it today
            // turns every request, 404s included, into a 500.
            //
            // The policy itself is defined in AuthorizationPolicies.DenyByDefault and is
            // unit-tested. Only its activation waits for §12 to register a scheme. §5's
            // Definition of Done tracks this as its one open item.

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
