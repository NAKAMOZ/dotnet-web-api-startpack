namespace Api.Models.Enums;

/// <summary>
/// What a <see cref="VerificationToken"/> authorises. One table serves every short-lived,
/// single-use, hashed-at-rest credential in the system; this discriminator is what keeps a
/// token minted for one purpose from being spent on another.
/// </summary>
/// <remarks>
/// The type is part of the lookup, never just a label read after the fact: a
/// password-reset token presented to the email-verification endpoint must not resolve.
/// Persisted as a string (§7) so reordering members cannot silently re-point existing rows.
/// </remarks>
public enum VerificationTokenType
{
    /// <summary>Confirms ownership of an email address. Sets <c>User.EmailVerified</c>.</summary>
    EmailVerification,

    /// <summary>
    /// Authorises a password reset. Consuming one bumps <c>User.SecurityStamp</c> and
    /// revokes every session (Authentication.md §13).
    /// </summary>
    PasswordReset,

    /// <summary>
    /// Bridges the two halves of an MFA login (Authentication.md §8). Proves the password
    /// step succeeded and authorises exactly one thing: completing this login.
    /// </summary>
    MfaChallenge,

    /// <summary>WebAuthn registration challenge, issued to an authenticated user.</summary>
    PasskeyRegistrationChallenge,

    /// <summary>
    /// WebAuthn authentication challenge. The only type whose
    /// <c>VerificationToken.UserId</c> is null — the ceremony starts before any user is
    /// known.
    /// </summary>
    PasskeyAuthenticationChallenge,

    /// <summary>Single-use OAuth state for a social authorization callback.</summary>
    SocialAuthorizationState,
}
