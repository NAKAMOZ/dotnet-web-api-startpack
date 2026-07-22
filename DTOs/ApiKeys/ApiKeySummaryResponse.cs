namespace Api.DTOs.ApiKeys;

/// <summary>
/// A key as it appears in <c>GET /api/v1/api-keys</c> — everything except the secret, which
/// no longer exists in a readable form anywhere.
/// </summary>
public sealed record ApiKeySummaryResponse
{
    public required Guid Id { get; init; }

    public required string Name { get; init; }

    /// <summary>The public segment only. Enough to match a key against a log line.</summary>
    public required string KeyPrefix { get; init; }

    public required IReadOnlyList<string> Scopes { get; init; }

    public DateTimeOffset? ExpiresAt { get; init; }

    /// <summary>Last successful authentication — the signal for spotting a key nobody uses.</summary>
    public DateTimeOffset? LastUsedAt { get; init; }

    public DateTimeOffset? RevokedAt { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }
}
