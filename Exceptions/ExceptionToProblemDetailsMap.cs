using Microsoft.AspNetCore.Mvc;

namespace Api.Exceptions;

/// <summary>
/// The single place an exception becomes an HTTP status and an error code.
/// </summary>
/// <remarks>
/// One table rather than a status per throw site. A service that constructs its own
/// response knows about HTTP, which it should not; and when the same failure is thrown from
/// three places it acquires three statuses, one of which is wrong.
/// <para>
/// §14's exception middleware is the only caller. §13 owns the mapping itself so the
/// catalogue in <c>Documentation/Errors.md</c> has exactly one source to stay in sync with.
/// </para>
/// </remarks>
public static class ExceptionToProblemDetailsMap
{
    /// <summary>
    /// Maps an exception to the status, code and title that describe it publicly.
    /// </summary>
    /// <remarks>
    /// <b>Nothing derived from the exception's own message reaches the caller.</b> The title
    /// comes from this table; the detail is supplied by the middleware and only in
    /// Development. An exception message can carry a connection string, a SQL fragment or a
    /// file path, and none of those are the client's business.
    /// </remarks>
    public static (int Status, string ErrorCode, string Title) Map(Exception exception) => exception switch
    {
        // ── Authentication ───────────────────────────────────────────────────────────
        //
        // One status, one code, one title for every credential failure. Unknown email,
        // wrong password, locked account and passwordless account all arrive here as
        // InvalidCredentialsException and are indistinguishable on the wire — which is the
        // whole point (Authentication.md §5).
        InvalidCredentialsException domain => (
            StatusCodes.Status401Unauthorized, domain.ErrorCode, "Authentication failed."),

        // Reached only if a service lets one escape. It should have been converted to
        // InvalidCredentialsException before responding — a distinct "account locked" reply
        // tells an attacker the address exists AND that their guessing is working. Mapped
        // to the identical 401 so the leak cannot happen even when the conversion is missed.
        AccountLockedException => (
            StatusCodes.Status401Unauthorized, new InvalidCredentialsException().ErrorCode, "Authentication failed."),

        // A revoked session, not a client error to correct. 401 so the client re-authenticates.
        // Kept precise internally for incident handling, deliberately collapsed on the
        // wire so refresh-token replay is not an oracle.
        TokenReuseDetectedException => (
            StatusCodes.Status401Unauthorized,
            new InvalidCredentialsException().ErrorCode,
            "Authentication failed."),

        InvalidTokenException domain => (
            StatusCodes.Status400BadRequest, domain.ErrorCode, "The token is invalid."),

        ForbiddenOperationException domain => (
            StatusCodes.Status403Forbidden, domain.ErrorCode, "Forbidden."),

        UnsupportedProviderException domain => (
            StatusCodes.Status400BadRequest, domain.ErrorCode, "Unsupported provider."),

        // ── Resource state ───────────────────────────────────────────────────────────
        //
        // 404 covers both "does not exist" and "exists but is not yours". Answering 403 for
        // the second confirms the resource exists, which lets an attacker enumerate ids by
        // reading status codes (Authorization.md §11).
        ResourceNotFoundException domain => (
            StatusCodes.Status404NotFound, domain.ErrorCode, "Not found."),

        EmailAlreadyRegisteredException domain => (
            StatusCodes.Status409Conflict, domain.ErrorCode, "Conflict."),

        ConflictException domain => (
            StatusCodes.Status409Conflict, domain.ErrorCode, "Conflict."),

        // A DomainException subclass with no entry above. 500 rather than a guessed status:
        // an unmapped domain failure is a gap in this table, and a 400 would hide it behind
        // something that looks like the client's fault.
        DomainException domain => (
            StatusCodes.Status500InternalServerError, domain.ErrorCode, "An unexpected error occurred."),

        // ── Infrastructure ───────────────────────────────────────────────────────────
        OperationCanceledException => (
            StatusCodes.Status499ClientClosedRequest, "request_cancelled", "The request was cancelled."),

        BadHttpRequestException => (
            StatusCodes.Status400BadRequest, ErrorCodes.MalformedRequest, "The request could not be read."),

        _ => (
            StatusCodes.Status500InternalServerError, ErrorCodes.InternalError, "An unexpected error occurred."),
    };

    /// <summary>
    /// Builds the Problem Details body for an exception.
    /// </summary>
    /// <param name="exception">The failure.</param>
    /// <param name="includeDetail">
    /// Development only. In every other environment the exception message is dropped —
    /// §13's security requirement, and the reason this is a parameter rather than a
    /// judgement made at the call site.
    /// </param>
    public static ProblemDetails ToProblemDetails(Exception exception, bool includeDetail)
    {
        var (status, errorCode, title) = Map(exception);

        var problem = new ProblemDetails
        {
            Status = status,
            Title = title,
            Type = ProblemTypes.For(errorCode),

            // A DomainException message is written for the client — "That email address is
            // already registered." Any other exception's message is written for us, and is
            // withheld outside Development.
            Detail = exception is DomainException domain
                ? domain.Message
                : includeDetail ? exception.Message : null,
        };

        problem.Extensions[ProblemDetailsExtensions.ErrorCode] = errorCode;

        return problem;
    }
}
