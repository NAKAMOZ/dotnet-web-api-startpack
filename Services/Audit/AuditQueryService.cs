using Api.Data;
using Api.DTOs.Admin;
using Api.DTOs.Common;
using Api.Mappings;
using Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Api.Services.Audit;

/// <summary>
/// Filtered, paged reads over the audit trail (§15).
/// </summary>
/// <remarks>
/// Every filter this exposes is backed by an index declared in
/// <c>AuditLogEntryConfiguration</c>, each one leading with its selective column and ending in
/// <c>OccurredAt</c> so the ordering is served by the same index rather than by a sort
/// (DataAccess.md §3). Adding a filter here without adding its index is how an incident query
/// becomes a sequential scan of the largest table in the schema.
/// </remarks>
public sealed class AuditQueryService(AppDbContext database) : IAuditQueryService
{
    /// <summary>
    /// The only sortable field, matching <c>AuditLogQueryValidator.SortableFields</c>. Audit
    /// rows are read chronologically; any other ordering would mean an index that exists to
    /// serve a query nobody asked for.
    /// </summary>
    private const string OccurredAtField = "occurredAt";

    public async Task<PagedResponse<AuditLogEntryResponse>> QueryAsync(
        AuditLogQuery query,
        CancellationToken cancellationToken = default)
    {
        // No tracking: nothing read here is ever written back, and the change tracker would
        // hold a page of entities for the rest of the request for nothing.
        var filtered = Filter(database.AuditLogEntries.AsNoTracking(), query);

        // Counted before paging, and it is the count of matches rather than of the page —
        // what TotalPages is computed from.
        var totalCount = await filtered.LongCountAsync(cancellationToken);

        var rows = await Sort(filtered, query.Sort)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);

        return new PagedResponse<AuditLogEntryResponse>
        {
            Items = rows.Select(entry => entry.ToResponse()).ToArray(),
            Page = query.Page,
            PageSize = query.PageSize,
            TotalCount = totalCount,
        };
    }

    private static IQueryable<AuditLogEntry> Filter(IQueryable<AuditLogEntry> source, AuditLogQuery query)
    {
        if (query.UserId is { } userId)
        {
            source = source.Where(entry => entry.UserId == userId);
        }

        if (query.EventType is { } eventType)
        {
            source = source.Where(entry => entry.EventType == eventType);
        }

        // Half-open interval: From inclusive, To exclusive. A closed upper bound makes
        // "today" and "yesterday" overlap at the boundary and double-count the events that
        // land exactly on it.
        if (query.From is { } from)
        {
            source = source.Where(entry => entry.OccurredAt >= from);
        }

        if (query.To is { } to)
        {
            source = source.Where(entry => entry.OccurredAt < to);
        }

        if (!string.IsNullOrEmpty(query.CorrelationId))
        {
            // Exact match, not a prefix or a contains: the id is generated or adopted whole
            // (§14), and a LIKE here would neither use the index nor answer a real question.
            source = source.Where(entry => entry.CorrelationId == query.CorrelationId);
        }

        return source;
    }

    /// <summary>
    /// Applies the sort expression against a one-element allow-list.
    /// </summary>
    /// <remarks>
    /// The field name is compared, never interpolated. §10's validator already rejects an
    /// unknown field with a 400, so anything reaching here is known — but the allow-list is
    /// re-applied rather than trusted, because "a validator elsewhere checked this" is how
    /// injection surfaces outlive the validator that was supposed to cover them.
    /// </remarks>
    private static IOrderedQueryable<AuditLogEntry> Sort(IQueryable<AuditLogEntry> source, string? sort)
    {
        // Newest first by default. An investigation starts at the most recent event, and a
        // page of the oldest rows in a 90-day trail is never the answer to the first question.
        if (string.IsNullOrEmpty(sort))
        {
            return source.OrderByDescending(entry => entry.OccurredAt);
        }

        var parts = sort.Split(':', 2, StringSplitOptions.TrimEntries);

        var ascending = parts.Length < 2
                        || !parts[1].Equals("desc", StringComparison.OrdinalIgnoreCase);

        if (!parts[0].Equals(OccurredAtField, StringComparison.OrdinalIgnoreCase))
        {
            return source.OrderByDescending(entry => entry.OccurredAt);
        }

        return ascending
            ? source.OrderBy(entry => entry.OccurredAt)
            : source.OrderByDescending(entry => entry.OccurredAt);
    }
}
