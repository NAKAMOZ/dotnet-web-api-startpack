using System.Globalization;
using System.Security.Claims;
using System.Threading.RateLimiting;
using Api.Configuration;
using Api.Filters;
using Api.Middleware;
using Api.Services.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace Api.Extensions;

public static partial class ServiceCollectionExtensions
{
    /// <summary>Registers §17's named endpoint policies and the default general limiter.</summary>
    public static IServiceCollection AddApiRateLimiting(this IServiceCollection services)
    {
        services.AddRateLimiter();
        services
            .AddOptions<Microsoft.AspNetCore.RateLimiting.RateLimiterOptions>()
            .Configure<IOptions<RateLimitOptions>, IRateLimiterPartitionFactory>(
                (limiter, configured, partitions) =>
                    ConfigureRateLimiting(limiter, configured.Value, partitions));

        services.AddSingleton<EmailTargetRateLimitFilter>();

        return services;
    }

    private static void ConfigureRateLimiting(
        Microsoft.AspNetCore.RateLimiting.RateLimiterOptions limiter,
        RateLimitOptions configured,
        IRateLimiterPartitionFactory partitions)
    {
        limiter.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
        limiter.OnRejected = WriteRejectionAsync;

        // Every input below is fixed once this method runs, so each limiter factory is built
        // here and reused. Written inline, the delegate would close over the policy name and
        // the limits, allocating a display class and a delegate per request that
        // PartitionedRateLimiter discards on every partition hit.
        var general = SlidingWindowFactory(
            partitions,
            RateLimitPolicies.General,
            configured.GeneralPermitLimit,
            configured.GeneralWindow,
            configured.GeneralSegmentsPerWindow);

        // The general policy is the default: every endpoint is covered without relying on a
        // controller author to remember an attribute. Named policies below add tighter caps.
        limiter.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
            RateLimitPartition.Get(GeneralPartitionKey(context), general));

        limiter.AddPolicy(
            RateLimitPolicies.AuthStrict,
            PerClientIpPolicy(
                partitions,
                RateLimitPolicies.AuthStrict,
                configured.AuthStrictPermitLimit,
                configured.AuthStrictWindow));

        limiter.AddPolicy(
            RateLimitPolicies.EmailSending,
            PerClientIpPolicy(
                partitions,
                RateLimitPolicies.EmailSending,
                configured.EmailSendingIpPermitLimit,
                configured.EmailSendingIpWindow));

        limiter.AddPolicy(
            RateLimitPolicies.Registration,
            PerClientIpPolicy(
                partitions,
                RateLimitPolicies.Registration,
                configured.RegistrationPermitLimit,
                configured.RegistrationWindow));
    }

    private static Func<HttpContext, RateLimitPartition<string>> PerClientIpPolicy(
        IRateLimiterPartitionFactory partitions,
        string policy,
        int permitLimit,
        TimeSpan window)
    {
        var factory = FixedWindowFactory(partitions, policy, permitLimit, window);
        return context => RateLimitPartition.Get(ClientIp(context), factory);
    }

    private static Func<string, RateLimiter> FixedWindowFactory(
        IRateLimiterPartitionFactory partitions,
        string policy,
        int permitLimit,
        TimeSpan window) =>
        key => partitions.CreateFixedWindow(policy, key, permitLimit, window);

    private static Func<string, RateLimiter> SlidingWindowFactory(
        IRateLimiterPartitionFactory partitions,
        string policy,
        int permitLimit,
        TimeSpan window,
        int segmentsPerWindow) =>
        key => partitions.CreateSlidingWindow(policy, key, permitLimit, window, segmentsPerWindow);

    private static async ValueTask WriteRejectionAsync(
        OnRejectedContext rejected,
        CancellationToken cancellationToken)
    {
        var httpContext = rejected.HttpContext;
        httpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;

        if (rejected.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
        {
            httpContext.Response.Headers.RetryAfter =
                Math.Max(1, (int)Math.Ceiling(retryAfter.TotalSeconds))
                    .ToString(CultureInfo.InvariantCulture);
        }

        var endpointPolicy = httpContext.GetEndpoint()?.Metadata
            .GetMetadata<EnableRateLimitingAttribute>()
            ?.PolicyName
            ?? RateLimitPolicies.General;

        var logger = httpContext.RequestServices
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger("Api.RateLimiting");

        logger.LogWarning(
            "Rate limit rejected request for policy {RateLimitPolicy} from partition {PartitionKey}",
            endpointPolicy,
            ClientIp(httpContext));

        var problemDetails = httpContext.RequestServices.GetRequiredService<IProblemDetailsService>();
        await problemDetails.WriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = RateLimitProblemDetails.Create(httpContext),
        });
    }

    private static string GeneralPartitionKey(HttpContext context)
    {
        var subject = context.User.FindFirstValue(ClaimTypes.NameIdentifier)
                      ?? context.User.FindFirstValue("sub");

        return string.IsNullOrWhiteSpace(subject)
            ? "ip:" + ClientIp(context)
            : "user:" + subject;
    }

    private static string ClientIp(HttpContext context) =>
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
}
