using Microsoft.AspNetCore.Authorization;

namespace Api.Handlers.Authorization;

/// <summary>
/// Policies that are not generated per permission.
/// </summary>
public static class AuthorizationPolicies
{
    /// <summary>
    /// The deny-by-default fallback: an endpoint carrying no authorization metadata
    /// requires an authenticated user, so a forgotten attribute fails closed. Anonymous
    /// endpoints opt out explicitly with <c>[AllowAnonymous]</c>.
    /// </summary>
    /// <remarks>
    /// <b>Defined here but not yet assigned to <c>AuthorizationOptions.FallbackPolicy</c>.</b>
    /// Assigning it requires an authentication scheme to exist, and none is registered
    /// until §12 — see the note in <c>ServiceCollectionExtensions.Authorization.cs</c>.
    /// Its behaviour is unit-tested independently of the pipeline.
    /// </remarks>
    public static AuthorizationPolicy DenyByDefault { get; } =
        new AuthorizationPolicyBuilder()
            .RequireAuthenticatedUser()
            .Build();
}
