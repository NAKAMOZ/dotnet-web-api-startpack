using System.Linq.Expressions;
using Api.DTOs.Common;
using Microsoft.EntityFrameworkCore;

namespace Api.Helpers;

/// <summary>
/// Applies paging and whitelisted sorting to a query, and materialises the
/// <see cref="PagedResponse{T}"/> envelope (§13).
/// </summary>
public static class PagedQueryExtensions
{
    /// <summary>
    /// Orders by one of an explicitly allowed set of fields.
    /// </summary>
    /// <param name="source">The query.</param>
    /// <param name="sort">
    /// <c>field</c> or <c>field:desc</c>, as validated by §10. Null or blank falls back to
    /// <paramref name="defaultSort"/>.
    /// </param>
    /// <param name="allowed">
    /// Field name → ordering expression. <b>An allow-list of typed expressions, never a
    /// string reaching the query.</b>
    /// </param>
    /// <param name="defaultSort">Applied when no sort is requested — a page needs a stable order.</param>
    /// <remarks>
    /// The dictionary is the security control. Because a caller's string only ever selects
    /// an expression written here, there is nothing to escape and nothing to sanitise: an
    /// unknown key has no expression and the request is rejected. Dynamic-LINQ or
    /// string-interpolated ORDER BY would both make the caller's text part of the query.
    /// <para>
    /// Beyond injection, an unrestricted sort orders by columns the caller cannot read.
    /// Sorting users by <c>PasswordHash</c> returns no hashes and still leaks information
    /// about them.
    /// </para>
    /// </remarks>
    public static IQueryable<T> ApplySort<T>(
        this IQueryable<T> source,
        string? sort,
        IReadOnlyDictionary<string, Expression<Func<T, object?>>> allowed,
        Expression<Func<T, object?>> defaultSort)
    {
        if (string.IsNullOrWhiteSpace(sort))
        {
            return source.OrderByDescending(defaultSort);
        }

        var parts = sort.Split(':', 2);

        if (!allowed.TryGetValue(parts[0], out var selector))
        {
            // Unreachable through the API — §10's validator rejects unknown fields before a
            // service is called. Kept because this method is also reachable from code that
            // has no validator in front of it, and failing loudly beats ordering by
            // something arbitrary.
            throw new ArgumentException($"Sort field '{parts[0]}' is not sortable.", nameof(sort));
        }

        var descending = parts.Length == 2
                         && parts[1].Equals("desc", StringComparison.OrdinalIgnoreCase);

        return descending ? source.OrderByDescending(selector) : source.OrderBy(selector);
    }

    /// <summary>
    /// Counts, pages and projects a query into a <see cref="PagedResponse{T}"/>.
    /// </summary>
    /// <remarks>
    /// The count runs against the <em>filtered</em> query before paging, so
    /// <c>TotalCount</c> describes the result set rather than the page. It is a second
    /// round trip by design: returning a page without a total forces the client to guess
    /// whether more exist.
    /// </remarks>
    public static async Task<PagedResponse<TResult>> ToPagedResponseAsync<TSource, TResult>(
        this IQueryable<TSource> source,
        PagedQuery query,
        Func<TSource, TResult> projection,
        CancellationToken cancellationToken)
    {
        var total = await source.LongCountAsync(cancellationToken);

        var rows = await source
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);

        return new PagedResponse<TResult>
        {
            Items = [.. rows.Select(projection)],
            Page = query.Page,
            PageSize = query.PageSize,
            TotalCount = total,
        };
    }
}
