using Api.DTOs.Auth;
using Api.Validators.Common;
using FluentValidation;

namespace Api.Validators.Auth;

internal sealed class RefreshRequestValidator : AbstractValidator<RefreshRequest>
{
    public RefreshRequestValidator()
    {
        // The token is optional here and that is not an oversight: in cookie mode it
        // arrives in __Secure-auth.refresh and the body is empty. Requiring it would break
        // every browser client. "Neither cookie nor body" is a 401 from the endpoint, not a
        // 400 from this validator — an absent credential is an authentication failure.
        RuleFor(request => request.RefreshToken)
            .MaximumLength(512)
            .WithErrorCode(ValidationErrorCodes.TooLong)
            .WithMessage("Refresh token is not valid.")
            .When(request => request.RefreshToken is not null);
    }
}
