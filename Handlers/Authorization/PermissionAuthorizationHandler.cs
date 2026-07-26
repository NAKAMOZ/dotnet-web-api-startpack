using System.Security.Claims;
using Api.Handlers.Authentication;
using Microsoft.AspNetCore.Authorization;

namespace Api.Handlers.Authorization;

/// <summary>
/// Checks a <see cref="PermissionRequirement"/> against the caller's <c>roles</c> claim
/// via <see cref="RolePermissionMap"/>.
/// </summary>
public sealed class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    /// <summary>Claim type carrying role names, matching the <c>roles</c> claim in Authentication.md §2.</summary>
    public const string RolesClaimType = "roles";

    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PermissionRequirement requirement)
    {
        // Never call context.Fail() here. Failing is sticky: it vetoes the whole
        // authorization pass even if another handler would have succeeded. Simply not
        // calling Succeed is what denies — and it composes correctly when an endpoint
        // carries several requirements.
        if (context.User.Identity?.IsAuthenticated != true)
        {
            return Task.CompletedTask;
        }

        if (context.User.HasClaim(claim => claim.Type == ApiKeyAuthenticationHandler.ApiKeyIdClaimType)
            && !context.User.HasClaim(
                ApiKeyAuthenticationHandler.ScopeClaimType,
                requirement.Permission))
        {
            return Task.CompletedTask;
        }

        var roles = context.User
            .FindAll(RolesClaimType)
            .Concat(context.User.FindAll(ClaimTypes.Role))
            .Select(static claim => claim.Value);

        if (RolePermissionMap.Grants(roles, requirement.Permission))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
