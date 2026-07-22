using Api.DTOs.Admin;
using Api.Validators.Common;
using FluentValidation;

namespace Api.Validators.Admin;

internal sealed class AdminUpdateUserRequestValidator : AbstractValidator<AdminUpdateUserRequest>
{
    public AdminUpdateUserRequestValidator()
    {
        RuleFor(request => request.DisplayName)
            .MaximumLength(100)
            .WithErrorCode(ValidationErrorCodes.TooLong)
            .WithMessage("Display name must be at most 100 characters.")
            .When(request => request.DisplayName is not null);

        // An all-null patch is rejected rather than treated as a no-op success. A request
        // that changes nothing but returns 200 reads to the caller — and to the audit trail
        // — as a change that happened.
        RuleFor(request => request)
            .Must(request => request.DisplayName is not null
                             || request.EmailVerified is not null
                             || request.Unlock is not null)
            .WithErrorCode(ValidationErrorCodes.Required)
            .WithMessage("At least one field must be provided.");
    }
}
