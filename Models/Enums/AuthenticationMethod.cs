namespace Api.Models.Enums;

/// <summary>
/// How a session authenticated. Projected into the access token's <c>amr</c> claim so
/// endpoints requiring recent or strong authentication can check it without a second
/// lookup (Authentication.md §2).
/// </summary>
public enum AuthenticationMethod
{
    /// <summary><c>pwd</c> — email and password.</summary>
    Password,

    /// <summary><c>otp</c> — TOTP second factor.</summary>
    Totp,

    /// <summary><c>recovery</c> — MFA recovery code. Weaker than <see cref="Totp"/>; a code was consumed.</summary>
    RecoveryCode,

    /// <summary><c>webauthn</c> — passkey assertion.</summary>
    Passkey,

    /// <summary><c>google</c> — Google social login.</summary>
    Google,

    /// <summary><c>github</c> — GitHub social login.</summary>
    GitHub,
}
