namespace Api.DTOs.ApiKeys;

/// <summary>
/// A newly created API key.
/// </summary>
/// <remarks>
/// <b><see cref="Key"/> appears here and nowhere else, ever.</b> Only the prefix and an
/// Argon2id hash of the secret are stored, so the list endpoint cannot return it and
/// neither can support. A client that fails to save it creates a new key.
/// </remarks>
public sealed record CreateApiKeyResponse
{
    public required Guid Id { get; init; }

    public required string Name { get; init; }

    /// <summary>
    /// The full credential, <c>ak_&lt;prefix&gt;_&lt;secret&gt;</c>. Returned exactly once,
    /// never logged, and never included in an error payload.
    /// </summary>
    public required string Key { get; init; }

    /// <summary>The public segment. Stored in plaintext, and what identifies the key afterwards.</summary>
    public required string KeyPrefix { get; init; }

    public required IReadOnlyList<string> Scopes { get; init; }

    public DateTimeOffset? ExpiresAt { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }
}
