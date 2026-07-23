using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Api.Logging;

namespace Api.Services.Audit;

/// <summary>
/// Turns an audit event's metadata object into the JSON stored in the <c>jsonb</c> column,
/// with credential-shaped fields redacted on the way (§15).
/// </summary>
/// <remarks>
/// The redaction pass runs over the serialized tree rather than over the source object, so it
/// sees exactly what is about to be stored — including fields contributed by anonymous types,
/// dictionaries and nested objects, none of which a reflection pass over a declared type
/// would reach.
/// <para>
/// <b>Why the audit table needs this at all.</b> Log sinks rotate; this table is kept for 90
/// days by policy and read by administrators through an HTTP endpoint. A token serialized
/// into <c>Metadata</c> is a credential in durable storage, retrievable over the API, in the
/// one place nobody thinks to check for one.
/// </para>
/// </remarks>
public static class AuditMetadataSerializer
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        // Matches the API's wire format (§3), so a field is spelled the same way in a
        // response body and in the audit row that recorded it.
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>
    /// Serializes and redacts. Returns null for null input — the column is nullable, and an
    /// empty <c>{}</c> would claim there was detail to record.
    /// </summary>
    public static string? Serialize(object? metadata)
    {
        if (metadata is null)
        {
            return null;
        }

        var node = JsonSerializer.SerializeToNode(metadata, SerializerOptions);

        if (node is null)
        {
            return null;
        }

        Redact(node);

        return node.ToJsonString(SerializerOptions);
    }

    private static void Redact(JsonNode node)
    {
        switch (node)
        {
            case JsonObject jsonObject:
                // Keys are materialised first: assigning to an entry mutates the object, and
                // mutating it while enumerating it throws.
                foreach (var propertyName in jsonObject.Select(property => property.Key).ToArray())
                {
                    RedactProperty(jsonObject, propertyName);
                }

                break;

            case JsonArray jsonArray:
                foreach (var element in jsonArray.OfType<JsonNode>())
                {
                    Redact(element);
                }

                break;
        }
    }

    private static void RedactProperty(JsonObject jsonObject, string propertyName)
    {
        if (SensitiveFieldNames.IsSecret(propertyName))
        {
            // The whole subtree goes, not just scalar leaves. A field called `tokens` holding
            // an object is exactly as disqualifying as one holding a string.
            jsonObject[propertyName] = SensitiveFieldNames.RedactedValue;
            return;
        }

        var value = jsonObject[propertyName];

        if (value is null)
        {
            return;
        }

        // Emails inside free-form metadata are masked. The typed columns of the trail are the
        // documented exception; this column is not typed, so it gets the logging rule.
        if (SensitiveFieldNames.IsEmail(propertyName)
            && value.GetValueKind() is JsonValueKind.String
            && value.GetValue<string>() is { } text)
        {
            jsonObject[propertyName] = SensitiveFieldNames.MaskEmail(text);
            return;
        }

        Redact(value);
    }
}
