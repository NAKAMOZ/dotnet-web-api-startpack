using Api.DTOs.Auth;
using Api.Validators.Common;
using FluentValidation;

namespace Api.Validators.Auth;

internal sealed class MfaLoginRequestValidator : AbstractValidator<MfaLoginRequest>
{
    public MfaLoginRequestValidator()
    {
        RuleFor(request => request.MfaTicket)
            .NotEmpty()
            .WithErrorCode(ValidationErrorCodes.Required)
            .WithMessage("An MFA ticket is required.")
            .MaximumLength(256)
            .WithErrorCode(ValidationErrorCodes.TooLong)
            .WithMessage("MFA ticket is not valid.");

        // Shape only, and loosely: a 6-digit TOTP code and a longer recovery code both
        // arrive here. Which one it is, and whether it verifies, is the service's business —
        // a validator that could tell them apart precisely would also tell an attacker
        // which factor an account uses.
        RuleFor(request => request.Code)
            .NotEmpty()
            .WithErrorCode(ValidationErrorCodes.Required)
            .WithMessage("A code is required.")
            .Length(6, 32)
            .WithErrorCode(ValidationErrorCodes.CodeMalformed)
            .WithMessage("Code is not valid.");
    }
}
