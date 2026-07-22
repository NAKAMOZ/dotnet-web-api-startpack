using Api.DTOs.Common;

namespace Api.DTOs.Admin;

/// <summary>Filters for <c>GET /api/v1/admin/users</c>, on top of the shared paging parameters.</summary>
public sealed record AdminUserListQuery : PagedQuery
{
    /// <summary>
    /// Free-text match on email and display name. Passed as a parameter to a LIKE, never
    /// interpolated — and the same allow-list rule governs <c>Sort</c>.
    /// </summary>
    public string? Search { get; init; }

    /// <summary>Restrict to holders of one role name.</summary>
    public string? Role { get; init; }

    public bool? EmailVerified { get; init; }

    /// <summary>Restrict to accounts currently locked out.</summary>
    public bool? Locked { get; init; }
}
