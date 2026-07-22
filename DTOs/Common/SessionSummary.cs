namespace Api.DTOs.Common;

/// <summary>
/// A session as an administrator sees it. The self-service view is
/// <c>DTOs/Sessions/SessionResponse</c>, which adds <c>IsCurrent</c> — a notion that only
/// exists relative to the caller's own request.
/// </summary>
public sealed record SessionSummary
{
    public required Guid Id { get; init; }

    /// <summary>Human-readable device label derived from the user agent.</summary>
    public string? DeviceLabel { get; init; }

    public string? IpAddress { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }

    public required DateTimeOffset LastActiveAt { get; init; }

    public required DateTimeOffset AbsoluteExpiresAt { get; init; }

    /// <summary>Null while the session is live.</summary>
    public DateTimeOffset? RevokedAt { get; init; }
}
