using Api.DTOs.PasswordReset;
using Api.Validators.Common;
using FluentValidation;

namespace Api.Validators.PasswordReset;

internal sealed class PasswordResetRequestValidator : AbstractValidator<PasswordResetRequest>
{
    public PasswordResetRequestValidator()
    {
        RuleFor(request => request.Email).Email();

        // Nothing here reveals whether the address is registered. The endpoint returns 202
        // either way — a 404 for unknown addresses would be an account-enumeration oracle
        // that needs no credentials at all.
    }
}
