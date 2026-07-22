using Api.DTOs.Passkeys;
using Api.Validators.Common;
using FluentValidation;

namespace Api.Validators.Passkeys;

internal sealed class PasskeyAuthenticationOptionsRequestValidator
    : AbstractValidator<PasskeyAuthenticationOptionsRequest>
{
    public PasskeyAuthenticationOptionsRequestValidator()
    {
        // Length only, and no format rule. This endpoint is anonymous, and a rejection that
        // depends on the address would make it an enumeration oracle — the response must
        // look the same whether or not the address exists, including when it is nonsense.
        RuleFor(request => request.Email)
            .MaximumLength(EmailRules.MaximumLength)
            .WithErrorCode(ValidationErrorCodes.EmailTooLong)
            .WithMessage($"Email must be at most {EmailRules.MaximumLength} characters.")
            .When(request => request.Email is not null);
    }
}
