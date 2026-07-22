using FluentValidation;

namespace Api.Validators.Common;

/// <summary>
/// The password policy, in one place.
/// </summary>
/// <remarks>
/// Registration, password reset and password change all call
/// <see cref="Password{T}(IRuleBuilder{T, string})"/>. That is the point: three endpoints
/// that each spell out their own rules drift, and the drift is invisible until an attacker
/// finds the weakest of the three.
/// <para>
/// The policy follows current NIST guidance (SP 800-63B): <b>length over composition</b>.
/// There is deliberately no "must contain an uppercase letter and a symbol" rule — those
/// rules measurably push users toward <c>Password1!</c>, which is why several such
/// passwords appear in this project's own deny list. Length, a deny list, and rejection of
/// predictable patterns do the work instead.
/// </para>
/// </remarks>
public static class PasswordRules
{
    /// <summary>Minimum length. Raising this later is safe; lowering it silently weakens every account created after.</summary>
    public const int MinimumLength = 12;

    /// <summary>
    /// Maximum length. Not a security limit — a cost limit.
    /// </summary>
    /// <remarks>
    /// Argon2id hashes whatever it is given, so an unbounded password is an unbounded
    /// amount of deliberately expensive work on an anonymous endpoint. 256 is far above any
    /// real passphrase and far below anything that hurts.
    /// </remarks>
    public const int MaximumLength = 256;

    /// <summary>Applies the full policy to a password property.</summary>
    public static IRuleBuilderOptions<T, string> Password<T>(this IRuleBuilder<T, string> rule) =>
        rule
            .NotEmpty()
                .WithErrorCode(ValidationErrorCodes.Required)
                .WithMessage("A password is required.")
            .MinimumLength(MinimumLength)
                .WithErrorCode(ValidationErrorCodes.PasswordTooShort)
                .WithMessage($"Password must be at least {MinimumLength} characters.")
            .MaximumLength(MaximumLength)
                .WithErrorCode(ValidationErrorCodes.PasswordTooLong)
                .WithMessage($"Password must be at most {MaximumLength} characters.")
            .Must(password => !PasswordDenyList.Contains(password))
                .WithErrorCode(ValidationErrorCodes.PasswordTooCommon)
                .WithMessage("This password is too common. Choose something less predictable.")
            .Must(password => !IsPredictablePattern(password))
                .WithErrorCode(ValidationErrorCodes.PasswordPredictablePattern)
                .WithMessage("This password is a repeated character or a simple sequence.");

    /// <summary>
    /// Additionally rejects a password containing the local part of the account's own email
    /// address — <c>nevzat@example.com</c> may not choose <c>nevzat-nevzat</c>.
    /// </summary>
    /// <remarks>
    /// Only usable where the request carries both values. Password reset cannot apply it:
    /// that request carries a token and a new password, and looking the address up to check
    /// would put a database call inside a validator that is meant to be structural. The
    /// service layer applies it there instead.
    /// </remarks>
    public static IRuleBuilderOptions<T, string> NotContainingEmail<T>(
        this IRuleBuilderOptions<T, string> rule,
        Func<T, string?> emailSelector) =>
        rule.Must((instance, password) =>
            {
                var email = emailSelector(instance);

                if (string.IsNullOrWhiteSpace(email))
                {
                    return true;
                }

                var localPart = email.Split('@')[0];

                // Very short local parts ("me", "hi") would reject far too much — a
                // password containing "me" is not thereby predictable.
                return localPart.Length < 4
                       || !password.Contains(localPart, StringComparison.OrdinalIgnoreCase);
            })
            .WithErrorCode(ValidationErrorCodes.PasswordContainsEmail)
            .WithMessage("Password must not contain your email address.");

    /// <summary>
    /// One repeated character, or a run along the alphabet, the digits, or a keyboard row —
    /// forwards or backwards.
    /// </summary>
    private static bool IsPredictablePattern(string password)
    {
        if (password.Length == 0)
        {
            return false;
        }

        if (password.All(character => character == password[0]))
        {
            return true;
        }

        string[] sequences =
        [
            "abcdefghijklmnopqrstuvwxyz",

            // Repeated so wrapping runs ("890123") are caught too.
            "01234567890123456789",

            // The keyboard rows joined into one run, not listed separately: users walk
            // straight from one row onto the next, and "qwertyuiopasdfghjkl" is a substring
            // of neither row on its own.
            "qwertyuiopasdfghjklzxcvbnm",
        ];

        var lowered = password.ToLowerInvariant();
        var reversed = new string(lowered.Reverse().ToArray());

        return sequences.Any(sequence =>
            sequence.Contains(lowered, StringComparison.Ordinal)
            || sequence.Contains(reversed, StringComparison.Ordinal));
    }
}
