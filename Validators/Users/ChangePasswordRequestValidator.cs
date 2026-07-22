using Api.DTOs.Users;
using Api.Validators.Common;
using FluentValidation;

namespace Api.Validators.Users;

internal sealed class ChangePasswordRequestValidator : AbstractValidator<ChangePasswordRequest>
{
    public ChangePasswordRequestValidator()
    {
        // The current password gets presence and bounds only — the same reasoning as login.
        // Grading it against the current policy would reject users whose password predates
        // that policy, on the very endpoint that exists to fix exactly that.
        RuleFor(request => request.CurrentPassword)
            .NotEmpty()
            .WithErrorCode(ValidationErrorCodes.Required)
            .WithMessage("Your current password is required.")
            .MaximumLength(PasswordRules.MaximumLength)
            .WithErrorCode(ValidationErrorCodes.PasswordTooLong)
            .WithMessage($"Password must be at most {PasswordRules.MaximumLength} characters.");

        RuleFor(request => request.NewPassword).Password();

        RuleFor(request => request.NewPassword)
            .NotEqual(request => request.CurrentPassword)
            .WithErrorCode(ValidationErrorCodes.PasswordUnchanged)
            .WithMessage("The new password must differ from the current one.");
    }
}
