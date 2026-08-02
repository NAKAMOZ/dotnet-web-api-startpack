namespace Api.Services.RateLimiting;

public interface IRedisRateLimitStore
{
    ValueTask<RedisRateLimitDecision> AcquireFixedWindowAsync(
        string key,
        int permitLimit,
        int permitCount,
        TimeSpan window,
        CancellationToken cancellationToken);

    ValueTask<RedisRateLimitDecision> AcquireSlidingWindowAsync(
        string key,
        int permitLimit,
        int permitCount,
        TimeSpan window,
        int segmentsPerWindow,
        CancellationToken cancellationToken);
}
