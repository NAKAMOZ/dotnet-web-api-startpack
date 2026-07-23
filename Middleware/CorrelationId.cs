namespace Api.Middleware;

/// <summary>
/// Where the correlation id lives while a request is in flight.
/// </summary>
/// <remarks>
/// The constants land in §13 because the Problem Details customisation reads them; §14 adds
/// the middleware that writes them. Sharing the names now avoids the alternative, which is
/// two string literals that agree until one of them is edited.
/// </remarks>
public static class CorrelationId
{
    /// <summary>
    /// Request and response header. A caller may supply one to stitch its own logs to ours.
    /// </summary>
    /// <remarks>
    /// Caller-supplied means attacker-supplied: §14's middleware must bound the length and
    /// strip anything that is not safe to write into a log line. Header injection through
    /// this value is on §22's negative-test list.
    /// </remarks>
    public const string HeaderName = "X-Correlation-Id";

    /// <summary>Key under which the resolved id is stored on <c>HttpContext.Items</c>.</summary>
    public const string ItemsKey = "CorrelationId";

    /// <summary>
    /// Longest inbound value accepted. Anything longer is replaced rather than truncated.
    /// </summary>
    /// <remarks>
    /// A bound is required because this value is written into every log line and every audit
    /// row for the request: without one, a caller can push a megabyte per request into the
    /// log pipeline for free.
    /// </remarks>
    public const int MaxLength = 64;

    /// <summary>
    /// Whether a caller-supplied value may be adopted as-is.
    /// </summary>
    /// <remarks>
    /// The allowed set — ASCII letters, digits, <c>-</c>, <c>_</c> and <c>.</c> — covers
    /// every id format a caller realistically arrives with (UUID, ULID, W3C trace id) and
    /// excludes the characters that make a log line lie: CR and LF forge new entries, and
    /// quotes and braces break JSON-sink framing. Kestrel already rejects CR/LF in header
    /// values; this is the second lock, because the value outlives the header — it reaches
    /// log sinks, the audit table and the response body.
    /// </remarks>
    public static bool IsWellFormed(string? value) =>
        !string.IsNullOrEmpty(value)
        && value.Length <= MaxLength
        && value.All(character =>
            char.IsAsciiLetterOrDigit(character) || character is '-' or '_' or '.');

    /// <summary>
    /// A fresh id for a request that supplied none — or supplied one that was not accepted.
    /// </summary>
    public static string New() => Guid.NewGuid().ToString("N");
}
