namespace Api.DTOs.Sessions;

/// <summary>
/// One of the caller's own sessions, for <c>GET /api/v1/sessions</c>.
/// </summary>
/// <remarks>
/// This list is a security feature, not a convenience: it is how a user notices a session
/// they did not create. That is why it carries device metadata rather than only ids.
/// </remarks>
public sealed record SessionResponse
{
    public required Guid Id { get; init; }

    /// <summary>Derived from the user agent — "Chrome on macOS", not the raw header.</summary>
    public string? DeviceLabel { get; init; }

    public string? IpAddress { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }

    public required DateTimeOffset LastActiveAt { get; init; }

    /// <summary>The 7-day cap, fixed at login and never extended by a refresh.</summary>
    public required DateTimeOffset AbsoluteExpiresAt { get; init; }

    /// <summary>
    /// Whether this is the session making the request. Exists so a client can warn before
    /// revoking it — and so "revoke all except current" has something to render against.
    /// </summary>
    public required bool IsCurrent { get; init; }
}
