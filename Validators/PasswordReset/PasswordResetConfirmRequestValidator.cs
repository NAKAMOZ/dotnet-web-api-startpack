using Api.DTOs.PasswordReset;
using Api.Validators.Common;
using FluentValidation;

namespace Api.Validators.PasswordReset;

internal sealed class PasswordResetConfirmRequestValidator : AbstractValidator<PasswordResetConfirmRequest>
{
    public PasswordResetConfirmRequestValidator()
    {
        RuleFor(request => request.Token)
            .NotEmpty()
            .WithErrorCode(ValidationErrorCodes.Required)
            .WithMessage("A reset token is required.")
            .MaximumLength(256)
            .WithErrorCode(ValidationErrorCodes.TooLong)
            .WithMessage("Reset token is not valid.");

        // The full policy — the same call register makes. The email-containment rule cannot
        // apply here because this request carries a token, not an address; §12 applies it
        // once the token resolves to a user.
        RuleFor(request => request.NewPassword).Password();
    }
}
