using Api.DTOs.Admin;
using Api.Validators.Common;
using FluentValidation;

namespace Api.Validators.Admin;

internal sealed class AssignRoleRequestValidator : AbstractValidator<AssignRoleRequest>
{
    public AssignRoleRequestValidator()
    {
        // Structural only. Whether the role exists is a lookup, and a 404 from the endpoint
        // says it more accurately than a 400 from here.
        RuleFor(request => request.RoleId)
            .NotEmpty()
            .WithErrorCode(ValidationErrorCodes.Required)
            .WithMessage("A role id is required.");
    }
}
