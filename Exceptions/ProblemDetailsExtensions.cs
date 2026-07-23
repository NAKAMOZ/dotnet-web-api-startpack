namespace Api.Exceptions;

/// <summary>
/// Names of the RFC 9457 extension members this API adds to every problem response.
/// </summary>
/// <remarks>
/// Constants rather than literals because they appear in three unrelated places — the
/// customisation callback, the validation filter, and the integration assertions — and a
/// typo in any one of them produces a response that looks right and is missing a field.
/// </remarks>
public static class ProblemDetailsExtensions
{
    /// <summary>Stable machine-readable code. The thing a client branches on.</summary>
    public const string ErrorCode = "errorCode";

    /// <summary>Per-field validation codes, parallel to the standard <c>errors</c> member.</summary>
    public const string ErrorCodes = "errorCodes";

    /// <summary>
    /// The caller-supplied or server-generated correlation id (§14).
    /// </summary>
    /// <remarks>
    /// The single most useful field in a support conversation: it is what turns "it failed
    /// this morning" into a specific request in the logs and a specific row in the audit
    /// trail.
    /// </remarks>
    public const string CorrelationId = "correlationId";

    /// <summary>The ASP.NET Core trace identifier, for matching against distributed traces (§28).</summary>
    public const string TraceId = "traceId";
}
