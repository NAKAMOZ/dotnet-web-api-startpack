namespace Api.DTOs.ApiKeys;

/// <summary>Body for <c>POST /api/v1/api-keys</c>.</summary>
public sealed record CreateApiKeyRequest
{
    /// <summary>Label shown in the key list, so a key can be recognised before revoking it.</summary>
    public required string Name { get; init; }

    /// <summary>
    /// Permission constants the key may exercise.
    /// </summary>
    /// <remarks>
    /// A key can never exceed its creator's own permissions: the effective set is the
    /// intersection of these scopes and the owner's role-granted permissions, evaluated at
    /// request time rather than frozen at creation (Authorization.md §7). Requesting a scope
    /// the caller does not hold is rejected here as well, so the mistake surfaces at
    /// creation rather than as a mysterious 403 later.
    /// </remarks>
    public required IReadOnlyList<string> Scopes { get; init; }

    /// <summary>Optional expiry. Null means the key lives until revoked.</summary>
    public DateTimeOffset? ExpiresAt { get; init; }
}
