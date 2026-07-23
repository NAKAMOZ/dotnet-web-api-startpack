using Api.Exceptions;
using Microsoft.AspNetCore.Diagnostics;

namespace Api.Middleware;

/// <summary>
/// Turns every unhandled exception into the one RFC 9457 envelope (§13/§14).
/// </summary>
/// <remarks>
/// An <see cref="IExceptionHandler"/> rather than a hand-written middleware, despite the
/// name the roadmap gives the file. The framework's <c>UseExceptionHandler</c> already owns
/// the hard parts — restoring the pipeline state, not double-writing a started response,
/// re-throwing when no handler claims the exception — and reimplementing them is how a
/// pipeline acquires a second, subtly different error path.
/// <para>
/// It maps nothing itself: <see cref="ExceptionToProblemDetailsMap"/> is the single table
/// (§13), and this class only decides how the result is logged and written.
/// </para>
/// </remarks>
public sealed class ExceptionHandlingMiddleware(
    IProblemDetailsService problemDetailsService,
    IHostEnvironment environment,
    ILogger<ExceptionHandlingMiddleware> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var (status, errorCode, _) = ExceptionToProblemDetailsMap.Map(exception);
        var correlationId = httpContext.Items[CorrelationId.ItemsKey] as string;

        // ── The client is gone ───────────────────────────────────────────────────────
        //
        // A cancelled request is not a fault and must not be logged as one — otherwise a
        // user navigating away becomes an error-rate spike and drowns the real ones. There
        // is also nothing to write: the socket is closed. The status is set anyway so the
        // request is not recorded as a 200 in access logs.
        if (status == StatusCodes.Status499ClientClosedRequest)
        {
            logger.LogDebug(
                "Request {Method} {Path} was cancelled before completion. CorrelationId: {CorrelationId}",
                httpContext.Request.Method,
                httpContext.Request.Path,
                correlationId);

            if (!httpContext.Response.HasStarted)
            {
                httpContext.Response.StatusCode = status;
            }

            return true;
        }

        // Server faults are logged with the exception; expected domain failures are not.
        // A stack trace for "that email is already registered" is noise, and noise in an
        // error log is what makes a real fault invisible.
        if (status >= StatusCodes.Status500InternalServerError)
        {
            logger.LogError(
                exception,
                "Unhandled exception for {Method} {Path}. ErrorCode: {ErrorCode}. CorrelationId: {CorrelationId}",
                httpContext.Request.Method,
                httpContext.Request.Path,
                errorCode,
                correlationId);
        }
        else
        {
            logger.LogInformation(
                "Request {Method} {Path} failed with {Status} ({ErrorCode}). CorrelationId: {CorrelationId}",
                httpContext.Request.Method,
                httpContext.Request.Path,
                status,
                errorCode,
                correlationId);
        }

        // Nothing can be rewritten once the first byte is out. Returning false re-throws,
        // which aborts the connection — an honest truncated response beats a valid-looking
        // body appended to a half-written one.
        if (httpContext.Response.HasStarted)
        {
            logger.LogWarning(
                "The response had already started; the error body could not be written. CorrelationId: {CorrelationId}",
                correlationId);

            return false;
        }

        httpContext.Response.StatusCode = status;

        // Written through IProblemDetailsService, not serialized here. That is what runs the
        // CustomizeProblemDetails callback registered in §13 — the one that attaches the
        // correlation id and the trace id, and strips the framework's `exception` extension
        // outside Development. Serializing directly would bypass all of it.
        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = ExceptionToProblemDetailsMap.ToProblemDetails(
                exception,
                environment.IsDevelopment()),
            Exception = exception,
        });
    }
}
