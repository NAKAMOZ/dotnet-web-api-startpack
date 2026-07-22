using Api.Attributes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace Api.Handlers.Authorization;

/// <summary>
/// Materialises a policy per permission on demand, so adding a permission never means
/// registering a policy by hand.
/// </summary>
/// <remarks>
/// Policies named <c>perm:&lt;permission&gt;</c> are generated here; everything else falls
/// through to the default provider, which owns the statically registered policies
/// (including <see cref="RequireRecentAuthAttribute.PolicyName"/>) and the fallback policy.
/// </remarks>
public sealed class PermissionPolicyProvider : IAuthorizationPolicyProvider
{
    private readonly DefaultAuthorizationPolicyProvider _fallback;

    public PermissionPolicyProvider(IOptions<AuthorizationOptions> options) =>
        _fallback = new DefaultAuthorizationPolicyProvider(options);

    public Task<AuthorizationPolicy> GetDefaultPolicyAsync() => _fallback.GetDefaultPolicyAsync();

    /// <summary>
    /// The deny-by-default policy applied to endpoints carrying no authorization metadata.
    /// Delegated to the default provider, which reads what
    /// <c>AddAuthorizationServices</c> configured.
    /// </summary>
    public Task<AuthorizationPolicy?> GetFallbackPolicyAsync() => _fallback.GetFallbackPolicyAsync();

    public Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        if (!policyName.StartsWith(RequirePermissionAttribute.PolicyPrefix, StringComparison.Ordinal))
        {
            return _fallback.GetPolicyAsync(policyName);
        }

        var permission = policyName[RequirePermissionAttribute.PolicyPrefix.Length..];

        // An unrecognised permission must not silently produce a policy that nobody can
        // satisfy — that fails closed but hides a typo until someone notices a permanent
        // 403. Failing at request time with a clear error surfaces it immediately, and the
        // startup validation in AddAuthorizationServices catches it earlier still.
        if (!Permissions.All.Contains(permission, StringComparer.Ordinal))
        {
            throw new InvalidOperationException(
                $"Unknown permission '{permission}'. Add it to {nameof(Permissions)} and grant it in {nameof(RolePermissionMap)}.");
        }

        var policy = new AuthorizationPolicyBuilder()
            .RequireAuthenticatedUser()
            .AddRequirements(new PermissionRequirement(permission))
            .Build();

        return Task.FromResult<AuthorizationPolicy?>(policy);
    }
}
