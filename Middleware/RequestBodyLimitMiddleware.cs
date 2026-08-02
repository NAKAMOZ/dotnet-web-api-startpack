using Api.Configuration;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Options;

namespace Api.Middleware;

/// <summary>Applies one body-size bound to known-length and streamed requests.</summary>
public sealed class RequestBodyLimitMiddleware(
    RequestDelegate next,
    IOptions<RequestSecurityOptions> configured)
{
    public Task InvokeAsync(HttpContext context)
    {
        var limit = configured.Value.MaxRequestBodySizeBytes;

        // Reject a known oversized body before routing, authentication or JSON parsing.
        if (context.Request.ContentLength > limit)
        {
            throw new BadHttpRequestException(
                "The request body exceeded the configured limit.",
                StatusCodes.Status413PayloadTooLarge);
        }

        // Kestrel enforces this while reading chunked/HTTP2 bodies whose length was not
        // declared. The feature is read-only after consumption begins, hence this early
        // middleware position below the exception handler and above every body reader.
        var feature = context.Features.Get<IHttpMaxRequestBodySizeFeature>();

        if (feature is { IsReadOnly: false })
        {
            feature.MaxRequestBodySize = limit;
        }

        return next(context);
    }
}
