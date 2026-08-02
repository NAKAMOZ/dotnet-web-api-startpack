namespace Api.Exceptions;

/// <summary>
/// Error codes not owned by a <see cref="DomainException"/> — the ones produced by the
/// framework, the pipeline, or middleware rather than by a service.
/// </summary>
/// <remarks>
/// Every code the API can emit is catalogued in <c>Documentation/Errors.md</c>: the ones
/// here, the ones each <see cref="DomainException"/> subclass carries, and the per-field
/// validation codes in <c>Validators/Common/ValidationErrorCodes.cs</c>.
/// <para>
/// Codes are the contract; titles and details are prose that may be reworded. A client
/// branching on the message string breaks on the first copy edit.
/// </para>
/// </remarks>
public static class ErrorCodes
{
    /// <summary>Request body or query failed structural validation. Per-field codes are in <c>errorCodes</c>.</summary>
    public const string ValidationFailed = "validation_failed";

    /// <summary>No credentials, or credentials that did not authenticate. Always accompanied by <c>WWW-Authenticate</c>.</summary>
    public const string Unauthorized = "unauthorized";

    /// <summary>Authenticated, but the caller lacks the required permission.</summary>
    public const string Forbidden = "forbidden";

    /// <summary>
    /// Authenticated and permitted, but not <em>recently</em> authenticated.
    /// </summary>
    /// <remarks>
    /// Distinct from <see cref="Forbidden"/> on purpose: a client seeing this should prompt
    /// for re-authentication, whereas a plain 403 means the caller will never be allowed and
    /// prompting would loop (Authentication.md §14).
    /// </remarks>
    public const string StepUpRequired = "step_up_required";

    /// <summary>Body could not be parsed, or a value could not be bound to its type.</summary>
    public const string MalformedRequest = "malformed_request";

    /// <summary>Request body exceeded the configured parser/resource bound.</summary>
    public const string PayloadTooLarge = "payload_too_large";

    /// <summary>
    /// A cookie-authenticated state-changing request arrived without a valid CSRF token (§14).
    /// </summary>
    /// <remarks>
    /// Distinct from <see cref="Forbidden"/> because the remediation differs: the caller is
    /// permitted to perform the operation and merely failed to prove the request was
    /// deliberate. A client seeing this should fetch <c>GET /api/v1/auth/csrf</c> and retry;
    /// a client seeing <see cref="Forbidden"/> should not retry at all.
    /// </remarks>
    public const string CsrfValidationFailed = "csrf_validation_failed";

    /// <summary>Rate limit exceeded (§17). Accompanied by <c>Retry-After</c>.</summary>
    public const string RateLimited = "rate_limited";

    /// <summary>
    /// Unhandled fault. The only code whose <c>detail</c> is deliberately uninformative —
    /// an exception message can carry a connection string, a SQL fragment, or a file path.
    /// </summary>
    public const string InternalError = "internal_error";
}
