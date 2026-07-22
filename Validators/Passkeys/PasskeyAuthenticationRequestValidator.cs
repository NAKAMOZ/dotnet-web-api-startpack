using System.Text.Json;
using Api.DTOs.Passkeys;
using Api.Validators.Common;
using FluentValidation;

namespace Api.Validators.Passkeys;

internal sealed class PasskeyAuthenticationRequestValidator : AbstractValidator<PasskeyAuthenticationRequest>
{
    public PasskeyAuthenticationRequestValidator()
    {
        RuleFor(request => request.AssertionResponse)
            .Must(response => response.ValueKind == JsonValueKind.Object)
            .WithErrorCode(ValidationErrorCodes.Required)
            .WithMessage("An assertion response object is required.");
    }
}
