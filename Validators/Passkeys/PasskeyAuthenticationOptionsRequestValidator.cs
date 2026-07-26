using Api.DTOs.Passkeys;
using FluentValidation;

namespace Api.Validators.Passkeys;

internal sealed class PasskeyAuthenticationOptionsRequestValidator
    : AbstractValidator<PasskeyAuthenticationOptionsRequest>
{
}
