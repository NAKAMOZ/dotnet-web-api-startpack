namespace Api.DTOs.PasswordReset;

/// <summary>Body for <c>POST /api/v1/password-reset/request</c>.</summary>
/// <remarks>
/// The endpoint returns <c>202 Accepted</c> for every syntactically valid address, whether
/// or not an account exists. A 404 for unknown addresses would turn this into an account
/// enumeration oracle — and one that needs no credentials at all.
/// </remarks>
public sealed record PasswordResetRequest
{
    public required string Email { get; init; }
}
