namespace Api.DTOs.Auth;

/// <summary>
/// Enumeration-safe acknowledgement of a registration request.
/// </summary>
/// <remarks>
/// The body is deliberately constant for a new and an existing address. Returning a user
/// id, the normalized email, or account state would turn registration into an account
/// enumeration endpoint.
/// </remarks>
public sealed record RegisterResponse
{
    public required string Message { get; init; }
}
