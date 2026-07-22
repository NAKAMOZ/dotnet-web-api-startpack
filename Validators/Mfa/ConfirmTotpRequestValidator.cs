using Api.DTOs.Mfa;
using Api.Validators.Common;
using FluentValidation;

namespace Api.Validators.Mfa;

internal sealed class ConfirmTotpRequestValidator : AbstractValidator<ConfirmTotpRequest>
{
    public ConfirmTotpRequestValidator()
    {
        // Exactly six digits here, unlike the login endpoint: enrolment confirmation accepts
        // a TOTP code and nothing else, so there is no recovery-code shape to leave room for.
        RuleFor(request => request.Code)
            .NotEmpty()
            .WithErrorCode(ValidationErrorCodes.Required)
            .WithMessage("A code is required.")
            .Matches("^[0-9]{6}$")
            .WithErrorCode(ValidationErrorCodes.CodeMalformed)
            .WithMessage("Code must be six digits.");
    }
}
