using Api.DTOs.Auth;
using Api.Validators.Common;
using FluentValidation;

namespace Api.Validators.Auth;

internal sealed class RegisterRequestValidator : AbstractValidator<RegisterRequest>
{
    public RegisterRequestValidator()
    {
        RuleFor(request => request.Email).Email();

        // The one endpoint that can apply the email rule: it is the only request carrying
        // both the address and the password.
        RuleFor(request => request.Password)
            .Password()
            .NotContainingEmail(request => request.Email);

        RuleFor(request => request.DisplayName)
            .MaximumLength(100)
            .WithErrorCode(ValidationErrorCodes.TooLong)
            .WithMessage("Display name must be at most 100 characters.");

        // Email uniqueness is NOT checked here. It needs the database, and a validator that
        // queries is neither fast nor side-effect-free — but more importantly, "this email
        // is taken" is an enumeration oracle. §12 handles the collision without telling the
        // caller which of the two things went wrong.
    }
}
