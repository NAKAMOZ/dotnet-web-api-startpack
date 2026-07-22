using Api.DTOs.Passkeys;
using Api.Validators.Common;
using FluentValidation;

namespace Api.Validators.Passkeys;

internal sealed class PasskeyRegistrationOptionsRequestValidator
    : AbstractValidator<PasskeyRegistrationOptionsRequest>
{
    public PasskeyRegistrationOptionsRequestValidator()
    {
        RuleFor(request => request.Label)
            .MaximumLength(100)
            .WithErrorCode(ValidationErrorCodes.TooLong)
            .WithMessage("Label must be at most 100 characters.")
            .When(request => request.Label is not null);
    }
}
