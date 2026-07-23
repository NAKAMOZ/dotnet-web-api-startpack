using System.Text.Json;
using Api.DTOs.Admin;
using Api.Models;

namespace Api.Mappings;

/// <summary>
/// Manual entity → DTO mapping for the audit trail (ADR-0009 — no AutoMapper).
/// </summary>
public static class AuditMappingExtensions
{
    /// <summary>
    /// Maps one stored row to its response shape.
    /// </summary>
    /// <remarks>
    /// Every column is carried across, which is unusual in this codebase and correct here:
    /// the audit row has no secret-bearing column to leave behind, and an administrator
    /// investigating an incident needs the whole row rather than a curated view of it. The
    /// one field that could carry something it should not — <c>Metadata</c> — was redacted on
    /// the way in by <c>AuditMetadataSerializer</c>, not on the way out. Redacting at read
    /// time would leave the credential in the table.
    /// </remarks>
    public static AuditLogEntryResponse ToResponse(this AuditLogEntry entry) => new()
    {
        Id = entry.Id,
        UserId = entry.UserId,
        EventType = entry.EventType,
        IpAddress = entry.IpAddress,
        UserAgent = entry.UserAgent,
        CorrelationId = entry.CorrelationId,
        Metadata = ParseMetadata(entry.Metadata),
        OccurredAt = entry.OccurredAt,
    };

    /// <summary>
    /// The column is <c>jsonb</c> but EF reads it as a string, so it is re-parsed into a
    /// <see cref="JsonElement"/> to reach the client as JSON rather than as a quoted string
    /// containing JSON.
    /// </summary>
    /// <remarks>
    /// <c>Clone()</c> is required: the <see cref="JsonDocument"/> owns pooled memory and is
    /// disposed here, and a <see cref="JsonElement"/> pointing into a disposed document throws
    /// when the response is serialized — a failure that shows up as a 500 on exactly the rows
    /// that have metadata.
    /// <para>
    /// A row whose metadata is not parseable is served with a null instead of failing the
    /// page. Every writer goes through <c>AuditMetadataSerializer</c>, so unparseable means
    /// hand-edited or corrupted — and losing one field beats failing an incident query.
    /// </para>
    /// </remarks>
    private static JsonElement? ParseMetadata(string? metadata)
    {
        if (string.IsNullOrWhiteSpace(metadata))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(metadata);

            return document.RootElement.Clone();
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
