namespace Api.Exceptions;

/// <summary>
/// The request is valid but conflicts with the current state — enrolling MFA twice,
/// verifying an already-verified address, unlinking the last remaining credential.
/// </summary>
/// <remarks>
/// Carries its own code rather than sharing one, because these are the cases a client can
/// actually act on: "you already have MFA enabled" is useful, unlike the deliberately vague
/// authentication failures.
/// </remarks>
public sealed class ConflictException(string errorCode, string message)
    : DomainException(errorCode, message);
