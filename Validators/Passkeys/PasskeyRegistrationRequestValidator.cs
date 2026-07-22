using System.Text.Json;
using Api.DTOs.Passkeys;
using Api.Validators.Common;
using FluentValidation;

namespace Api.Validators.Passkeys;

internal sealed class PasskeyRegistrationRequestValidator : AbstractValidator<PasskeyRegistrationRequest>
{
    public PasskeyRegistrationRequestValidator()
    {
        // Structural only: the payload must be a JSON object. Its contents are a WebAuthn
        // attestation, and validating that here would mean re-implementing the ceremony
        // Fido2NetLib already performs — twice, differently, is how the two end up
        // disagreeing about what is acceptable.
        RuleFor(request => request.AttestationResponse)
            .Must(response => response.ValueKind == JsonValueKind.Object)
            .WithErrorCode(ValidationErrorCodes.Required)
            .WithMessage("An attestation response object is required.");

        RuleFor(request => request.Label)
            .MaximumLength(100)
            .WithErrorCode(ValidationErrorCodes.TooLong)
            .WithMessage("Label must be at most 100 characters.")
            .When(request => request.Label is not null);
    }
}
