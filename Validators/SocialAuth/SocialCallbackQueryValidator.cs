using Api.DTOs.SocialAuth;
using Api.Validators.Common;
using FluentValidation;

namespace Api.Validators.SocialAuth;

internal sealed class SocialCallbackQueryValidator : AbstractValidator<SocialCallbackQuery>
{
    public SocialCallbackQueryValidator()
    {
        // State is required even when the provider reports an error. It is what ties this
        // callback to an authorize request this server issued; without it the endpoint is
        // an open door that processes whatever a redirect hands it.
        RuleFor(query => query.State)
            .NotEmpty()
            .WithErrorCode(ValidationErrorCodes.StateMissing)
            .WithMessage("The state parameter is required.")
            .MaximumLength(1024)
            .WithErrorCode(ValidationErrorCodes.TooLong)
            .WithMessage("The state parameter is not valid.");

        RuleFor(query => query)
            .Must(query => !string.IsNullOrWhiteSpace(query.Code) || !string.IsNullOrWhiteSpace(query.Error))
            .WithErrorCode(ValidationErrorCodes.CallbackIncomplete)
            .WithMessage("The callback must carry either an authorization code or an error.");

        RuleFor(query => query.Code)
            .MaximumLength(2048)
            .WithErrorCode(ValidationErrorCodes.TooLong)
            .WithMessage("The authorization code is not valid.")
            .When(query => query.Code is not null);

        RuleFor(query => query.Error)
            .MaximumLength(256)
            .WithErrorCode(ValidationErrorCodes.TooLong)
            .WithMessage("The error parameter is not valid.")
            .When(query => query.Error is not null);
    }
}
