namespace Api.DTOs.Common;

/// <summary>
/// The minimal view of an account, embedded in login and refresh responses so a client can
/// render a session without a second call.
/// </summary>
/// <remarks>
/// Deliberately small. Every field added here is returned on every successful
/// authentication, to every client, including ones that only wanted a token.
/// </remarks>
public sealed record UserSummary
{
    public required Guid Id { get; init; }

    public required string Email { get; init; }

    public required bool EmailVerified { get; init; }

    public string? DisplayName { get; init; }

    /// <summary>Role names. The same values the access token's <c>roles</c> claim carries.</summary>
    public required IReadOnlyList<string> Roles { get; init; }
}
