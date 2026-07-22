using System.Text.Json.Serialization;

namespace Api.DTOs.Auth;

/// <summary>
/// A rotated token pair. Returned by refresh, which — unlike login — has nothing new to say
/// about the account, so it carries no user summary.
/// </summary>
/// <remarks>
/// The refresh token here is a <b>new</b> value; the presented one is now spent and
/// presenting it again is treated as theft (Authentication.md §7). Both fields are null in
/// cookie mode, where the values are rewritten to their cookies instead.
/// </remarks>
public sealed record TokenPairResponse
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? AccessToken { get; init; }

    /// <summary>The successor refresh token. Stored only as a hash; returned exactly once.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? RefreshToken { get; init; }

    public required string TokenType { get; init; }

    public required DateTimeOffset ExpiresAt { get; init; }
}
