namespace Api.DTOs.Common;

/// <summary>
/// One page of results, with the metadata a client needs to request the next.
/// </summary>
/// <typeparam name="T">The response DTO for one row. Never an entity type.</typeparam>
public sealed record PagedResponse<T>
{
    public required IReadOnlyList<T> Items { get; init; }

    public required int Page { get; init; }

    public required int PageSize { get; init; }

    /// <summary>Total matching rows, not the count on this page.</summary>
    public required long TotalCount { get; init; }

    /// <summary>Derived, so it cannot disagree with the values above.</summary>
    public int TotalPages => PageSize <= 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
}
