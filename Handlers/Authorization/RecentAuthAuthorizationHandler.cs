using Api.Configuration;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace Api.Handlers.Authorization;

/// <summary>
/// Enforces the step-up window by reading <c>auth_time</c> — see Authentication.md §14.
/// </summary>
/// <remarks>
/// Reads <c>auth_time</c>, never <c>iat</c>. <c>iat</c> moves forward on every refresh, so
/// a stolen session would satisfy this check indefinitely; <c>auth_time</c> only advances
/// on a real re-authentication. This distinction is the entire control.
/// </remarks>
public sealed class RecentAuthAuthorizationHandler : AuthorizationHandler<RecentAuthRequirement>
{
    /// <summary>Claim carrying the epoch seconds at which the user last authenticated.</summary>
    public const string AuthTimeClaimType = "auth_time";

    private readonly TimeProvider _timeProvider;
    private readonly IOptionsMonitor<AuthSessionOptions> _sessionOptions;

    public RecentAuthAuthorizationHandler(
        TimeProvider timeProvider,
        IOptionsMonitor<AuthSessionOptions> sessionOptions)
    {
        _timeProvider = timeProvider;
        _sessionOptions = sessionOptions;
    }

    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        RecentAuthRequirement requirement)
    {
        var claim = context.User.FindFirst(AuthTimeClaimType);

        // No auth_time means no human authenticated on this credential. API keys land here
        // by design (Authentication.md §15) — they can never satisfy step-up.
        if (claim is null || !long.TryParse(claim.Value, out var epochSeconds))
        {
            return Task.CompletedTask;
        }

        var authenticatedAt = DateTimeOffset.FromUnixTimeSeconds(epochSeconds);
        var age = _timeProvider.GetUtcNow() - authenticatedAt;

        // A negative age means auth_time is in the future — a clock problem or a forged
        // claim. Treat it as not recent rather than as very recent.
        if (age >= TimeSpan.Zero && age < _sessionOptions.CurrentValue.RecentAuthenticationWindow)
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
