using System.Text.Json.Serialization;
using Api.DTOs.Common;

namespace Api.DTOs.Auth;

/// <summary>
/// A completed authentication: tokens plus enough of the account to render a session.
/// Also the response for MFA completion, social callback and passkey assertion — every
/// path that ends in a live session returns this shape.
/// </summary>
/// <remarks>
/// <b>The token fields are null in cookie mode</b>, where the same values are written to
/// <c>__Host-auth.access</c> and <c>__Secure-auth.refresh</c> instead. The server never
/// issues tokens in both places at once (ADR-0003) — a token in the body of a cookie-mode
/// response is a copy the client is expected to store somewhere, which is the risk cookie
/// mode exists to avoid.
/// </remarks>
public sealed record LoginResponse
{
    /// <summary>The JWT. Null in cookie mode.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? AccessToken { get; init; }

    /// <summary>
    /// The opaque refresh token, returned exactly once. Null in cookie mode.
    /// </summary>
    /// <remarks>
    /// Only the SHA-256 hash of this value is stored (ADR-0001). It cannot be re-read, and
    /// it must never appear in a log line.
    /// </remarks>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? RefreshToken { get; init; }

    /// <summary>Always <c>Bearer</c>. Present in both modes so clients can branch on transport, not on nulls.</summary>
    public required string TokenType { get; init; }

    /// <summary>Access-token expiry. Fifteen minutes out, and the upper bound on revocation lag.</summary>
    public required DateTimeOffset ExpiresAt { get; init; }

    public required UserSummary User { get; init; }
}
