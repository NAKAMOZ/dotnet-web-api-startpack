using System.Text.Json.Serialization;

namespace Api.DTOs.Auth;

/// <summary>Body for <c>POST /api/v1/auth/refresh</c>.</summary>
/// <remarks>
/// Optional, because in cookie mode the token arrives in <c>__Secure-auth.refresh</c> and
/// the body is empty. The endpoint reads the cookie first and falls back to this field;
/// presenting both is not an error, but they must match.
/// </remarks>
public sealed record RefreshRequest
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? RefreshToken { get; init; }
}
