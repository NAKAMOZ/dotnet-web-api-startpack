using Microsoft.AspNetCore.Authorization;

namespace Api.Attributes;

/// <summary>
/// Requires that the user authenticated within
/// <c>AuthSessionOptions.RecentAuthenticationWindow</c> — the step-up control described in
/// <c>Documentation/Architecture/Authentication.md</c> §14.
/// </summary>
/// <remarks>
/// Applies to destructive self-service operations and every administrative mutation.
/// These are what an attacker does after stealing a live session, so a valid access token
/// alone must not authorise them. API keys carry no human <c>auth_time</c> and therefore
/// cannot satisfy this policy.
/// <para>
/// <c>PUT /users/me/password</c> deliberately does <b>not</b> carry this attribute — it
/// requires the current password, which is the stronger proof.
/// </para>
/// </remarks>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
public sealed class RequireRecentAuthAttribute : AuthorizeAttribute
{
    /// <summary>Name of the statically registered step-up policy.</summary>
    public const string PolicyName = "RecentAuth";

    public RequireRecentAuthAttribute() => Policy = PolicyName;
}
