using StackExchange.Redis;

namespace Api.Services.RateLimiting;

/// <summary>
/// The Redis half of the limiter, seamed so the Lua scripts can be tested against a real
/// server. Keys arrive fully qualified — <see cref="RedisRateLimiterPartitionFactory"/> builds
/// them once per partition rather than per acquire.
/// </summary>
public interface IRedisRateLimitStore
{
    ValueTask<RedisRateLimitDecision> AcquireFixedWindowAsync(
        RedisKey key,
        int permitLimit,
        int permitCount,
        TimeSpan window,
        CancellationToken cancellationToken);

    ValueTask<RedisRateLimitDecision> AcquireSlidingWindowAsync(
        RedisKey key,
        int permitLimit,
        int permitCount,
        TimeSpan window,
        int segmentsPerWindow,
        CancellationToken cancellationToken);
}
