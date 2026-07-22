namespace Api.DTOs.Admin;

/// <summary>
/// One row of the administrative user list.
/// </summary>
/// <remarks>
/// Wider than the self-service profile — an administrator can see lockout state and
/// verification status — but it still carries no hash, no stamp, and no token material.
/// A reflection guard test asserts that (§9/§20).
/// </remarks>
public sealed record AdminUserResponse
{
    public required Guid Id { get; init; }

    public required string Email { get; init; }

    public required bool EmailVerified { get; init; }

    public string? DisplayName { get; init; }

    public required IReadOnlyList<string> Roles { get; init; }

    /// <summary>Non-null while the account is locked out. Visible to admins, never to the account itself.</summary>
    public DateTimeOffset? LockoutEndsAt { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }
}
