using FluentValidation;

namespace Api.Validators.Common;

/// <summary>Email format rules, shared by every endpoint that takes an address.</summary>
public static class EmailRules
{
    /// <summary>
    /// RFC 5321's practical maximum, and the length of the <c>citext</c> column.
    /// </summary>
    public const int MaximumLength = 254;

    /// <summary>
    /// Format plus length. Deliberately permissive on format.
    /// </summary>
    /// <remarks>
    /// Aggressive email regexes reject valid addresses — plus-addressing, apostrophes, new
    /// TLDs, internationalised domains — and buy nothing: the only real proof that an
    /// address exists and belongs to the registrant is the verification email, which this
    /// API sends anyway. The rule here exists to catch typos and to bound the input, not to
    /// adjudicate RFC 5322.
    /// </remarks>
    public static IRuleBuilderOptions<T, string> Email<T>(this IRuleBuilder<T, string> rule) =>
        rule
            .NotEmpty()
                .WithErrorCode(ValidationErrorCodes.Required)
                .WithMessage("An email address is required.")
            .MaximumLength(MaximumLength)
                .WithErrorCode(ValidationErrorCodes.EmailTooLong)
                .WithMessage($"Email must be at most {MaximumLength} characters.")
            .EmailAddress()
                .WithErrorCode(ValidationErrorCodes.EmailInvalid)
                .WithMessage("Email address is not valid.")
            .Must(email => email.Trim() == email)
                .WithErrorCode(ValidationErrorCodes.EmailInvalid)
                .WithMessage("Email address must not have leading or trailing whitespace.");
}
