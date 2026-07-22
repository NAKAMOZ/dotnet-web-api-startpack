using Api.DTOs.EmailVerification;
using Api.Validators.Common;
using FluentValidation;

namespace Api.Validators.EmailVerification;

internal sealed class ConfirmEmailRequestValidator : AbstractValidator<ConfirmEmailRequest>
{
    public ConfirmEmailRequestValidator()
    {
        // Presence and length. Whether the token exists, has expired, or was already spent
        // is a lookup, and all three answers converge on one response so the endpoint
        // cannot be used to probe which tokens are real.
        RuleFor(request => request.Token)
            .NotEmpty()
            .WithErrorCode(ValidationErrorCodes.Required)
            .WithMessage("A verification token is required.")
            .MaximumLength(256)
            .WithErrorCode(ValidationErrorCodes.TooLong)
            .WithMessage("Verification token is not valid.");
    }
}
