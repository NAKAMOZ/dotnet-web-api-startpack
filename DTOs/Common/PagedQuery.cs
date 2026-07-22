namespace Api.DTOs.Common;

/// <summary>
/// Paging and sorting parameters shared by every list endpoint.
/// </summary>
/// <remarks>
/// Bounds are enforced by a FluentValidation validator (§10), not by clamping here. A
/// silently clamped page size answers a different question than the one asked — the client
/// believes it received page 500 and it received page 1.
/// </remarks>
public record PagedQuery
{
    /// <summary>1-based page number.</summary>
    public int Page { get; init; } = 1;

    /// <summary>Rows per page. §10 caps this — an uncapped page size is a denial-of-service knob.</summary>
    public int PageSize { get; init; } = 20;

    /// <summary>
    /// Sort expression, <c>field</c> or <c>field:desc</c>.
    /// </summary>
    /// <remarks>
    /// Resolved against an allow-list of field names in the service layer, never
    /// concatenated into SQL or passed to a dynamic-LINQ evaluator. Sort-field injection is
    /// on §22's negative-test list.
    /// </remarks>
    public string? Sort { get; init; }
}
