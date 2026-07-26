using Api.Exceptions;
using Microsoft.AspNetCore.Mvc;

namespace Api.Middleware;

/// <summary>Builds the one RFC 9457 response shared by middleware and account limiting.</summary>
public static class RateLimitProblemDetails
{
    public static ProblemDetails Create(HttpContext httpContext)
    {
        var problem = new ProblemDetails
        {
            Status = StatusCodes.Status429TooManyRequests,
            Title = "Too many requests.",
            Type = ProblemTypes.For(ErrorCodes.RateLimited),
            Detail = "The request limit for this operation has been exceeded. Retry later.",
        };

        problem.Extensions[ProblemDetailsExtensions.ErrorCode] = ErrorCodes.RateLimited;
        problem.Extensions[ProblemDetailsExtensions.TraceId] = httpContext.TraceIdentifier;

        if (httpContext.Items.TryGetValue(CorrelationId.ItemsKey, out var correlationId))
        {
            problem.Extensions[ProblemDetailsExtensions.CorrelationId] = correlationId;
        }

        return problem;
    }
}
