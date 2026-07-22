using Api.DTOs.Auth;
using Api.Validators.Common;
using FluentValidation;

namespace Api.Validators.Auth;

internal sealed class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        // Presence and bounds only — deliberately NOT the password policy.
        //
        // Applying PasswordRules here would tell an attacker that a submitted password
        // cannot be the stored one before any credential check runs, and it would lock out
        // every user whose password predates a policy change. A login checks credentials;
        // it does not grade them.
        RuleFor(request => request.Email)
            .NotEmpty()
            .WithErrorCode(ValidationErrorCodes.Required)
            .WithMessage("An email address is required.")
            .MaximumLength(EmailRules.MaximumLength)
            .WithErrorCode(ValidationErrorCodes.EmailTooLong)
            .WithMessage($"Email must be at most {EmailRules.MaximumLength} characters.");

        RuleFor(request => request.Password)
            .NotEmpty()
            .WithErrorCode(ValidationErrorCodes.Required)
            .WithMessage("A password is required.")
            .MaximumLength(PasswordRules.MaximumLength)
            .WithErrorCode(ValidationErrorCodes.PasswordTooLong)
            .WithMessage($"Password must be at most {PasswordRules.MaximumLength} characters.");
    }
}
