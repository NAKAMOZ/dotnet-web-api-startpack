using Microsoft.AspNetCore.Authorization;

namespace Api.Handlers.Authorization;

/// <summary>
/// Requires that <c>auth_time</c> is within the configured step-up window
/// (Authentication.md §14).
/// </summary>
public sealed class RecentAuthRequirement : IAuthorizationRequirement;
