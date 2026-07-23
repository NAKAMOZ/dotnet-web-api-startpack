using Api.Exceptions;
using Api.Middleware;

namespace Api.Extensions;

public static partial class ServiceCollectionExtensions
{
    /// <summary>
    /// RFC 9457 Problem Details with this API's extension members (§13).
    /// </summary>
    /// <remarks>
    /// Applies to every problem response the framework produces — a 401 from the
    /// authorization middleware, a 404 from routing, a 415 from content negotiation — not
    /// only to the ones this codebase writes deliberately. That is the point: the envelope
    /// has to be uniform, and the responses easiest to forget are the ones nobody wrote.
    /// </remarks>
    public static IServiceCollection AddProblemDetailsStandards(this IServiceCollection services)
    {
        services.AddProblemDetails(options =>
            options.CustomizeProblemDetails = context =>
            {
                var problem = context.ProblemDetails;
                var http = context.HttpContext;

                // Status-derived codes for responses nobody threw an exception for. A 401
                // produced by the authorization middleware would otherwise carry no code at
                // all, and a client cannot branch on a blank.
                if (!problem.Extensions.ContainsKey(ProblemDetailsExtensions.ErrorCode))
                {
                    problem.Extensions[ProblemDetailsExtensions.ErrorCode] = CodeForStatus(problem.Status);
                }

                var errorCode = problem.Extensions[ProblemDetailsExtensions.ErrorCode] as string;

                if (!string.IsNullOrEmpty(errorCode))
                {
                    // Overwrites the framework's default type, which points at the RFC's own
                    // section for the status class — technically valid and useless, since it
                    // documents HTTP rather than this error.
                    problem.Type = ProblemTypes.For(errorCode);
                }

                problem.Extensions[ProblemDetailsExtensions.TraceId] = http.TraceIdentifier;

                if (http.Items.TryGetValue(CorrelationId.ItemsKey, out var correlationId))
                {
                    problem.Extensions[ProblemDetailsExtensions.CorrelationId] = correlationId;
                }

                // ── Never leak internals outside Development ─────────────────────────
                //
                // The framework's exception handler adds an "exception" extension carrying
                // the message, the type name and the stack trace. It is invaluable locally
                // and catastrophic in production: stack traces name internal paths,
                // dependency versions and query shapes, and the message may carry a
                // connection string outright.
                if (!http.RequestServices.GetRequiredService<IHostEnvironment>().IsDevelopment())
                {
                    problem.Extensions.Remove("exception");

                    if (problem.Status >= StatusCodes.Status500InternalServerError)
                    {
                        problem.Detail = null;
                    }
                }
            });

        return services;
    }

    /// <summary>
    /// Fallback code for framework-produced responses that carry no exception.
    /// </summary>
    /// <remarks>
    /// Only statuses the pipeline can produce on its own are listed. Everything else falls
    /// through to a generic code rather than being invented per status — an unlisted status
    /// reaching here means a response shape nobody catalogued, and a made-up code would hide
    /// that from the catalogue test.
    /// </remarks>
    private static string CodeForStatus(int? status) => status switch
    {
        StatusCodes.Status400BadRequest => ErrorCodes.MalformedRequest,
        StatusCodes.Status401Unauthorized => ErrorCodes.Unauthorized,
        StatusCodes.Status403Forbidden => ErrorCodes.Forbidden,
        StatusCodes.Status404NotFound => "not_found",
        StatusCodes.Status405MethodNotAllowed => "method_not_allowed",
        StatusCodes.Status406NotAcceptable => "not_acceptable",
        StatusCodes.Status415UnsupportedMediaType => "unsupported_media_type",
        StatusCodes.Status429TooManyRequests => ErrorCodes.RateLimited,

        // The §11 stubs. Mapped so they do not report themselves as internal_error, which
        // reads as a fault rather than as "this endpoint has no implementation yet".
        StatusCodes.Status501NotImplemented => "not_implemented",

        _ => ErrorCodes.InternalError,
    };
}
