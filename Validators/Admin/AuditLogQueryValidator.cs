using Api.DTOs.Admin;
using Api.Validators.Common;
using FluentValidation;

namespace Api.Validators.Admin;

internal sealed class AuditLogQueryValidator : AbstractValidator<AuditLogQuery>
{
    /// <summary>
    /// Audit rows are read chronologically. Sorting by anything else would mean an index
    /// that exists only to serve a query nobody asked for (DataAccess.md §3).
    /// </summary>
    public static readonly string[] SortableFields = ["occurredAt"];

    public AuditLogQueryValidator()
    {
        this.ApplyPagingRules();
        this.ApplySortRules(SortableFields);

        RuleFor(query => query.CorrelationId)
            .MaximumLength(64)
            .WithErrorCode(ValidationErrorCodes.TooLong)
            .WithMessage("Correlation id must be at most 64 characters.")
            .When(query => query.CorrelationId is not null);

        // An inverted range silently returns nothing, which reads as "no such events" —
        // exactly the wrong answer to give someone investigating an incident.
        RuleFor(query => query)
            .Must(query => query.From is null || query.To is null || query.From <= query.To)
            .WithErrorCode(ValidationErrorCodes.RangeInverted)
            .WithMessage("'From' must not be later than 'To'.");

        // The enum itself is the allow-list: an out-of-range value fails model binding
        // before this validator runs, so an unknown event type is a 400 rather than an empty
        // page that looks like an answer.
        RuleFor(query => query.EventType)
            .IsInEnum()
            .WithErrorCode(ValidationErrorCodes.OutOfRange)
            .WithMessage("Unknown audit event type.")
            .When(query => query.EventType is not null);
    }
}
