namespace Api.Exceptions;

/// <summary>
/// A verification, reset, MFA or challenge token did not resolve.
/// </summary>
/// <remarks>
/// One exception for unknown, expired, already-consumed and wrong-type, on purpose. The
/// four are separately meaningful to the server and identical to the client: telling a
/// caller "this token expired" rather than "no such token" confirms the token was once
/// real, which is information only its holder should have.
/// </remarks>
public sealed class InvalidTokenException()
    : DomainException("invalid_token", "The token is invalid or has expired.");
