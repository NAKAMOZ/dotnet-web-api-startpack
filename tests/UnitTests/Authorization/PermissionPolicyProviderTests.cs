using Api.Attributes;
using Api.Handlers.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.Extensions.Options;

namespace UnitTests.Authorization;

public class PermissionPolicyProviderTests
{
    private static PermissionPolicyProvider CreateProvider(
        Action<AuthorizationOptions>? configure = null)
    {
        var options = new AuthorizationOptions();
        configure?.Invoke(options);
        return new PermissionPolicyProvider(Options.Create(options));
    }

    [Fact]
    public async Task GeneratesAPolicyForAKnownPermission()
    {
        var provider = CreateProvider();

        var policy = await provider.GetPolicyAsync(
            RequirePermissionAttribute.PolicyPrefix + Permissions.AuditRead);

        Assert.NotNull(policy);
        Assert.Contains(policy.Requirements, r =>
            r is PermissionRequirement p && p.Permission == Permissions.AuditRead);
    }

    [Fact]
    public async Task GeneratedPolicyAlsoRequiresAuthentication()
    {
        var provider = CreateProvider();

        var policy = await provider.GetPolicyAsync(
            RequirePermissionAttribute.PolicyPrefix + Permissions.UsersReadAny);

        Assert.NotNull(policy);
        Assert.Contains(policy.Requirements, static r => r is DenyAnonymousAuthorizationRequirement);
    }

    [Fact]
    public async Task ThrowsForAnUnknownPermission()
    {
        // A typo must not quietly become a policy nobody can satisfy — that fails closed
        // but presents as a permanent, unexplained 403.
        var provider = CreateProvider();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            provider.GetPolicyAsync(RequirePermissionAttribute.PolicyPrefix + "users:reed:any"));

        Assert.Contains("users:reed:any", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DelegatesNonPermissionPolicyNamesToTheDefaultProvider()
    {
        var provider = CreateProvider(o => o.AddPolicy(
            RequireRecentAuthAttribute.PolicyName,
            p => p.RequireAuthenticatedUser().AddRequirements(new RecentAuthRequirement())));

        var policy = await provider.GetPolicyAsync(RequireRecentAuthAttribute.PolicyName);

        Assert.NotNull(policy);
        Assert.Contains(policy.Requirements, static r => r is RecentAuthRequirement);
    }

    [Fact]
    public void DenyByDefaultPolicyRequiresAnAuthenticatedUser()
    {
        // §5's deny-by-default guarantee, verified independently of the pipeline: the
        // authorization middleware cannot run until an authentication scheme exists (§12),
        // so the policy is asserted directly.
        Assert.Contains(
            AuthorizationPolicies.DenyByDefault.Requirements,
            static r => r is DenyAnonymousAuthorizationRequirement);
    }

    [Fact]
    public async Task SurfacesWhateverFallbackIsConfigured()
    {
        var provider = CreateProvider(o => o.FallbackPolicy = AuthorizationPolicies.DenyByDefault);

        var fallback = await provider.GetFallbackPolicyAsync();

        Assert.NotNull(fallback);
        Assert.Contains(fallback.Requirements, static r => r is DenyAnonymousAuthorizationRequirement);
    }
}
