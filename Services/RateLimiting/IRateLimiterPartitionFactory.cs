using System.Threading.RateLimiting;

namespace Api.Services.RateLimiting;

/// <summary>Creates one limiter for a cached policy/partition pair.</summary>
public interface IRateLimiterPartitionFactory
{
    RateLimiter CreateFixedWindow(
        string policy,
        string partitionKey,
        int permitLimit,
        TimeSpan window);

    RateLimiter CreateSlidingWindow(
        string policy,
        string partitionKey,
        int permitLimit,
        TimeSpan window,
        int segmentsPerWindow);
}
