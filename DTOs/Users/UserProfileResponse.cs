namespace Api.DTOs.Users;

/// <summary>
/// The caller's own account — <c>GET /api/v1/users/me</c>.
/// </summary>
/// <remarks>
/// Resolved from the <c>sub</c> claim; the route takes no user id, which is why there is no
/// ownership check to get wrong (Authorization.md §5).
/// </remarks>
public sealed record UserProfileResponse
{
    public required Guid Id { get; init; }

    public required string Email { get; init; }

    public required bool EmailVerified { get; init; }

    public string? DisplayName { get; init; }

    public required IReadOnlyList<string> Roles { get; init; }

    /// <summary>Whether a <b>confirmed</b> TOTP credential exists. An unconfirmed enrolment does not count.</summary>
    public required bool MfaEnabled { get; init; }

    /// <summary>How many passkeys are registered. A count, not the credentials themselves.</summary>
    public required int PasskeyCount { get; init; }

    /// <summary>
    /// Whether the account has a password at all. Social- and passkey-only accounts do not,
    /// and a client that assumes otherwise renders a change-password form that cannot work.
    /// </summary>
    public required bool HasPassword { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }
}
