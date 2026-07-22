using Api.DTOs.Admin;
using Api.Extensions;
using Api.Validators.Common;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace UnitTests.Validators;

/// <summary>
/// Paging and sort allow-list behaviour on the two administrative query endpoints.
/// </summary>
public class QueryValidatorTests
{
    private readonly IValidator<AdminUserListQuery> _users;
    private readonly IValidator<AuditLogQuery> _auditLogs;

    public QueryValidatorTests()
    {
        var services = new ServiceCollection().AddValidationServices().BuildServiceProvider();
        _users = services.GetRequiredService<IValidator<AdminUserListQuery>>();
        _auditLogs = services.GetRequiredService<IValidator<AuditLogQuery>>();
    }

    [Theory]
    [InlineData("email")]
    [InlineData("createdAt:desc")]
    [InlineData("emailVerified:asc")]
    [InlineData(null)]
    public void AcceptsAllowedSortFields(string? sort) =>
        Assert.True(_users.Validate(new AdminUserListQuery { Sort = sort }).IsValid);

    [Theory]
    [InlineData("passwordHash")]                    // a column that is never returned at all
    [InlineData("securityStamp")]
    [InlineData("email; DROP TABLE auth.\"Users\"")]
    [InlineData("email:sideways")]
    public void RejectsSortFieldsOutsideTheAllowList(string sort)
    {
        // Sorting by a column the caller cannot read still leaks its ordering — which for a
        // hash column means leaking information about values the API never returns.
        var result = _users.Validate(new AdminUserListQuery { Sort = sort });

        Assert.Contains(ValidationErrorCodes.SortFieldNotAllowed, result.Errors.Select(e => e.ErrorCode));
    }

    [Theory]
    [InlineData(0, 20, ValidationErrorCodes.PageOutOfRange)]
    [InlineData(-1, 20, ValidationErrorCodes.PageOutOfRange)]
    [InlineData(1, 0, ValidationErrorCodes.PageSizeOutOfRange)]
    [InlineData(1, 101, ValidationErrorCodes.PageSizeOutOfRange)]
    public void RejectsOutOfRangePaging(int page, int pageSize, string expectedCode)
    {
        var result = _users.Validate(new AdminUserListQuery { Page = page, PageSize = pageSize });

        Assert.Contains(expectedCode, result.Errors.Select(e => e.ErrorCode));
    }

    [Fact]
    public void AcceptsTheMaximumPageSize() =>
        Assert.True(_users.Validate(new AdminUserListQuery { PageSize = PaginationRules.MaximumPageSize }).IsValid);

    [Fact]
    public void RejectsAnInvertedDateRange()
    {
        // An inverted range returns nothing, which reads as "no such events" — the wrong
        // answer to give someone investigating an incident.
        var query = new AuditLogQuery
        {
            From = new DateTimeOffset(2026, 7, 22, 0, 0, 0, TimeSpan.Zero),
            To = new DateTimeOffset(2026, 7, 21, 0, 0, 0, TimeSpan.Zero),
        };

        Assert.Contains(
            ValidationErrorCodes.RangeInverted,
            _auditLogs.Validate(query).Errors.Select(e => e.ErrorCode));
    }
}
