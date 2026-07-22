using Api.DTOs.Common;

namespace Api.DTOs.Admin;

/// <summary>
/// One user in full, for <c>GET /api/v1/admin/users/{userId}</c> — the list row plus the
/// security posture an administrator needs when investigating an account.
/// </summary>
public sealed record AdminUserDetailResponse
{
    public required Guid Id { get; init; }

    public required string Email { get; init; }

    public required bool EmailVerified { get; init; }

    public string? DisplayName { get; init; }

    public required IReadOnlyList<string> Roles { get; init; }

    public DateTimeOffset? LockoutEndsAt { get; init; }

    /// <summary>Consecutive failures since the last success. Resets to zero on a successful login.</summary>
    public required int FailedLoginCount { get; init; }

    public required bool MfaEnabled { get; init; }

    public required bool HasPassword { get; init; }

    /// <summary>Linked providers by name — <c>google</c>, <c>github</c>.</summary>
    public required IReadOnlyList<string> LinkedProviders { get; init; }

    /// <summary>Live sessions. The admin view, with no notion of "current".</summary>
    public required IReadOnlyList<SessionSummary> ActiveSessions { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }
}
