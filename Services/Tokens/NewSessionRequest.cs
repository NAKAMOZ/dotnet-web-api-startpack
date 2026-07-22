namespace Api.Services.Tokens;

/// <summary>
/// Device metadata captured at login. This is what makes "where am I logged in?"
/// answerable — itself a security feature, since it is how a user notices a session they
/// did not create (ADR-0002).
/// </summary>
public sealed record NewSessionRequest
{
    public required Guid UserId { get; init; }

    /// <summary>How this login authenticated. Carried into the access token's <c>amr</c>.</summary>
    public required IReadOnlyCollection<AuthenticationMethod> AuthenticationMethods { get; init; }

    /// <summary>Client IP as resolved by the forwarded-headers configuration (§16).</summary>
    public string? IpAddress { get; init; }

    /// <summary>Raw user agent. Untrusted input — must reach logs as a structured property, never concatenated.</summary>
    public string? UserAgent { get; init; }

    /// <summary>Human-readable label derived from the user agent, for the session list.</summary>
    public string? DeviceLabel { get; init; }
}
