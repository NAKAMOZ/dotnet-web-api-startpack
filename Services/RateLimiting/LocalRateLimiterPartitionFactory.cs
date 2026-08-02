using System.Threading.RateLimiting;

namespace Api.Services.RateLimiting;

/// <summary>Built-in in-memory limiters for a single-process local deployment.</summary>
public sealed class LocalRateLimiterPartitionFactory : IRateLimiterPartitionFactory
{
    public RateLimiter CreateFixedWindow(
        string policy,
        string partitionKey,
        int permitLimit,
        TimeSpan window) =>
        new FixedWindowRateLimiter(new FixedWindowRateLimiterOptions
        {
            AutoReplenishment = true,
            PermitLimit = permitLimit,
            QueueLimit = 0,
            Window = window,
        });

    public RateLimiter CreateSlidingWindow(
        string policy,
        string partitionKey,
        int permitLimit,
        TimeSpan window,
        int segmentsPerWindow) =>
        new SlidingWindowRateLimiter(new SlidingWindowRateLimiterOptions
        {
            AutoReplenishment = true,
            PermitLimit = permitLimit,
            QueueLimit = 0,
            SegmentsPerWindow = segmentsPerWindow,
            Window = window,
        });
}
