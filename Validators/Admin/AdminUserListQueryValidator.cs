using Api.DTOs.Admin;
using Api.Validators.Common;
using FluentValidation;

namespace Api.Validators.Admin;

internal sealed class AdminUserListQueryValidator : AbstractValidator<AdminUserListQuery>
{
    /// <summary>
    /// The only columns this endpoint will order by. An allow-list, not a block-list: a new
    /// column is unsortable until someone adds it here deliberately, which is the safe
    /// default when the alternative is ordering by <c>PasswordHash</c>.
    /// </summary>
    public static readonly string[] SortableFields = ["email", "createdAt", "emailVerified"];

    public AdminUserListQueryValidator()
    {
        this.ApplyPagingRules();
        this.ApplySortRules(SortableFields);

        RuleFor(query => query.Search)
            .MaximumLength(256)
            .WithErrorCode(ValidationErrorCodes.TooLong)
            .WithMessage("Search term must be at most 256 characters.")
            .When(query => query.Search is not null);

        RuleFor(query => query.Role)
            .MaximumLength(64)
            .WithErrorCode(ValidationErrorCodes.TooLong)
            .WithMessage("Role name must be at most 64 characters.")
            .When(query => query.Role is not null);
    }
}
