namespace Api.DTOs.Users;

/// <summary>
/// An external identity linked to the caller's account —
/// <c>GET /api/v1/users/me/accounts</c>.
/// </summary>
/// <remarks>
/// The provider's subject identifier is deliberately <b>not</b> returned. It is an
/// identifier at a third party that the client has no use for, and echoing it back only
/// widens what a leaked response discloses.
/// </remarks>
public sealed record LinkedAccountResponse
{
    /// <summary>Local id, for <c>DELETE /api/v1/users/me/accounts/{accountId}</c>.</summary>
    public required Guid Id { get; init; }

    /// <summary><c>google</c> or <c>github</c>.</summary>
    public required string Provider { get; init; }

    public required DateTimeOffset LinkedAt { get; init; }
}
