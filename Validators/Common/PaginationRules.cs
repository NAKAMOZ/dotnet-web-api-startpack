using Api.DTOs.Common;
using FluentValidation;

namespace Api.Validators.Common;

/// <summary>Paging and sorting rules for every list endpoint.</summary>
public static class PaginationRules
{
    /// <summary>
    /// Hard ceiling on rows per request.
    /// </summary>
    /// <remarks>
    /// An uncapped page size is a denial-of-service knob any authenticated caller can turn:
    /// one request for a million audit rows costs a full scan, a large serialization, and
    /// the memory for both.
    /// </remarks>
    public const int MaximumPageSize = 100;

    /// <summary>
    /// Applies page and page-size bounds. Rejects rather than clamps — a silently clamped
    /// page size answers a different question than the one asked, and the client never
    /// learns that it did.
    /// </summary>
    public static void ApplyPagingRules<T>(this AbstractValidator<T> validator)
        where T : PagedQuery
    {
        validator.RuleFor(query => query.Page)
            .GreaterThanOrEqualTo(1)
            .WithErrorCode(ValidationErrorCodes.PageOutOfRange)
            .WithMessage("Page must be 1 or greater.");

        validator.RuleFor(query => query.PageSize)
            .InclusiveBetween(1, MaximumPageSize)
            .WithErrorCode(ValidationErrorCodes.PageSizeOutOfRange)
            .WithMessage($"Page size must be between 1 and {MaximumPageSize}.");
    }

    /// <summary>
    /// Restricts <c>Sort</c> to an allow-list of field names, with an optional
    /// <c>:asc</c>/<c>:desc</c> suffix.
    /// </summary>
    /// <remarks>
    /// <b>An allow-list, never an escape or a sanitiser.</b> The sort field reaches an
    /// ORDER BY; anything that is not a name this endpoint published is rejected outright.
    /// Beyond injection, an unrestricted sort lets a caller order by columns they cannot
    /// read — sorting users by <c>PasswordHash</c> leaks information about values that are
    /// never returned. §22 covers sort-field probing.
    /// </remarks>
    public static void ApplySortRules<T>(this AbstractValidator<T> validator, params string[] allowedFields)
        where T : PagedQuery
    {
        validator.RuleFor(query => query.Sort)
            .Must(sort => IsAllowed(sort, allowedFields))
            .WithErrorCode(ValidationErrorCodes.SortFieldNotAllowed)
            .WithMessage($"Sort field must be one of: {string.Join(", ", allowedFields)}.");
    }

    private static bool IsAllowed(string? sort, string[] allowedFields)
    {
        if (string.IsNullOrWhiteSpace(sort))
        {
            return true;
        }

        var field = sort.Split(':')[0];

        return allowedFields.Contains(field, StringComparer.OrdinalIgnoreCase)
               && sort.Split(':') is { Length: <= 2 } parts
               && (parts.Length == 1 || parts[1] is "asc" or "desc");
    }
}
