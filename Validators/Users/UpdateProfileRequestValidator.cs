using Api.DTOs.Users;
using Api.Validators.Common;
using FluentValidation;

namespace Api.Validators.Users;

internal sealed class UpdateProfileRequestValidator : AbstractValidator<UpdateProfileRequest>
{
    public UpdateProfileRequestValidator()
    {
        RuleFor(request => request.DisplayName)
            .MaximumLength(100)
            .WithErrorCode(ValidationErrorCodes.TooLong)
            .WithMessage("Display name must be at most 100 characters.")
            .When(request => request.DisplayName is not null);
    }
}
