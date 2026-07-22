namespace Api.DTOs.Users;

/// <summary>
/// Body for <c>PATCH /api/v1/users/me</c>.
/// </summary>
/// <remarks>
/// Only the display name is writable. Email is deliberately not: changing it would move the
/// login identity and invalidate the verification state, so it belongs behind its own
/// verify-then-swap flow rather than inside a general profile patch (deferred, §29).
/// </remarks>
public sealed record UpdateProfileRequest
{
    public string? DisplayName { get; init; }
}
