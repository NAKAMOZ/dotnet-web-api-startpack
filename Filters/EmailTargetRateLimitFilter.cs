using System.Globalization;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Threading.RateLimiting;
using Api.Configuration;
using Api.DTOs.PasswordReset;
using Api.Middleware;
using Api.Services.RateLimiting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace Api.Filters;

/// <summary>
/// Adds the per-target-account half of <c>email-sending</c>. The middleware enforces the
/// per-IP half before authentication; this action filter runs after authentication and model
/// validation, where the target account can be identified without trusting raw token data.
/// </summary>
public sealed class EmailTargetRateLimitFilter : IAsyncActionFilter, IOrderedFilter, IDisposable
{
    private readonly PartitionedRateLimiter<string> _limiter;
    private readonly ILogger<EmailTargetRateLimitFilter> _logger;

    public EmailTargetRateLimitFilter(
        IOptions<RateLimitOptions> options,
        IRateLimiterPartitionFactory partitions,
        ILogger<EmailTargetRateLimitFilter> logger)
    {
        var configured = options.Value;
        _logger = logger;

        _limiter = PartitionedRateLimiter.Create<string, string>(target =>
            RateLimitPartition.Get(
                target,
                key => partitions.CreateFixedWindow(
                    RateLimitPolicies.EmailSending + "-account",
                    key,
                    configured.EmailSendingAccountPermitLimit,
                    configured.EmailSendingAccountWindow)));
    }

    /// <summary>
    /// Runs after the default-order validation filter. Invalid addresses must not consume a
    /// real account's email allowance.
    /// </summary>
    public int Order => 1_000;

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (!UsesEmailSendingPolicy(context) || TargetKey(context) is not { } targetKey)
        {
            await next();
            return;
        }

        using var lease = await _limiter.AcquireAsync(
            targetKey,
            permitCount: 1,
            context.HttpContext.RequestAborted);

        if (lease.IsAcquired)
        {
            await next();
            return;
        }

        var retryAfter = lease.TryGetMetadata(MetadataName.RetryAfter, out var delay)
            ? delay
            : TimeSpan.FromMinutes(1);

        context.HttpContext.Response.Headers.RetryAfter =
            Math.Max(1, (int)Math.Ceiling(retryAfter.TotalSeconds))
                .ToString(CultureInfo.InvariantCulture);

        _logger.LogWarning(
            "Rate limit rejected request for policy {RateLimitPolicy}",
            RateLimitPolicies.EmailSending);

        context.Result = new ObjectResult(RateLimitProblemDetails.Create(context.HttpContext))
        {
            StatusCode = StatusCodes.Status429TooManyRequests,
            ContentTypes = { "application/problem+json" },
        };
    }

    public void Dispose() => _limiter.Dispose();

    private static bool UsesEmailSendingPolicy(ActionExecutingContext context) =>
        context.HttpContext.GetEndpoint()
            ?.Metadata
            .GetMetadata<EnableRateLimitingAttribute>()
            ?.PolicyName == RateLimitPolicies.EmailSending;

    private static string? TargetKey(ActionExecutingContext context)
    {
        var email = context.ActionArguments.Values
            .OfType<PasswordResetRequest>()
            .Select(request => request.Email)
            .FirstOrDefault();

        if (!string.IsNullOrWhiteSpace(email))
        {
            // The limiter key persists in memory for the window. Hashing prevents the
            // victim's address from becoming durable diagnostic data or a heap-dump leak.
            var normalized = email.Trim().ToUpperInvariant();
            var digest = SHA256.HashData(Encoding.UTF8.GetBytes(normalized));
            return "email:" + Convert.ToHexString(digest);
        }

        var subject = context.HttpContext.User.FindFirstValue(ClaimTypes.NameIdentifier)
                      ?? context.HttpContext.User.FindFirstValue("sub");

        return string.IsNullOrWhiteSpace(subject) ? null : "user:" + subject;
    }
}
