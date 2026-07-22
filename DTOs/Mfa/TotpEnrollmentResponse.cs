namespace Api.DTOs.Mfa;

/// <summary>
/// Enrolment payload for <c>POST /api/v1/mfa/totp/enroll</c>.
/// </summary>
/// <remarks>
/// <b>The only response in the API that ever contains the TOTP secret.</b> It is encrypted
/// at rest and there is no endpoint that returns it again — a user who loses the
/// authenticator re-enrols rather than re-reading the secret. An endpoint that could
/// re-display it would turn a stolen session into a permanent second factor.
/// </remarks>
public sealed record TotpEnrollmentResponse
{
    /// <summary>Base32 shared secret, shown once. Never logged.</summary>
    public required string Secret { get; init; }

    /// <summary>
    /// The <c>otpauth://</c> URI an authenticator app scans. Contains
    /// <see cref="Secret"/> — same handling rules apply to the whole string.
    /// </summary>
    public required string OtpAuthUri { get; init; }

    /// <summary>
    /// Enrolment is not complete until a valid code is submitted to the confirm endpoint.
    /// An unconfirmed credential must not gate login, or a failed scan locks the user out
    /// of their own account.
    /// </summary>
    public required bool RequiresConfirmation { get; init; }
}
