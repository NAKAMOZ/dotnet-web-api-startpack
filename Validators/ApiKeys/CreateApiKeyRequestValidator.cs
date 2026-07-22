using Api.DTOs.ApiKeys;
using Api.Handlers.Authorization;
using Api.Validators.Common;
using FluentValidation;

namespace Api.Validators.ApiKeys;

internal sealed class CreateApiKeyRequestValidator : AbstractValidator<CreateApiKeyRequest>
{
    public CreateApiKeyRequestValidator(TimeProvider timeProvider)
    {
        RuleFor(request => request.Name)
            .NotEmpty()
            .WithErrorCode(ValidationErrorCodes.Required)
            .WithMessage("A name is required.")
            .MaximumLength(100)
            .WithErrorCode(ValidationErrorCodes.TooLong)
            .WithMessage("Name must be at most 100 characters.");

        RuleFor(request => request.Scopes)
            .NotEmpty()
            .WithErrorCode(ValidationErrorCodes.ScopesEmpty)
            .WithMessage("At least one scope is required.");

        // Every scope must be a known permission constant. An unknown one is rejected here
        // rather than stored and silently ignored — a key that appears to grant something it
        // does not is worse than one that fails to be created.
        //
        // This checks only that the scope EXISTS. Whether the caller may grant it is a
        // separate check in §12, because it needs the caller's own permissions: a key can
        // never exceed its creator's (Authorization.md §7).
        RuleForEach(request => request.Scopes)
            .Must(scope => Permissions.All.Contains(scope, StringComparer.Ordinal))
            .WithErrorCode(ValidationErrorCodes.ScopeUnknown)
            .WithMessage("'{PropertyValue}' is not a known permission.");

        RuleFor(request => request.ExpiresAt)
            .GreaterThan(_ => timeProvider.GetUtcNow())
            .WithErrorCode(ValidationErrorCodes.ExpiryInPast)
            .WithMessage("Expiry must be in the future.")
            .When(request => request.ExpiresAt is not null);
    }
}
