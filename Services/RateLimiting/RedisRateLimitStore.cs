using Api.Configuration;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace Api.Services.RateLimiting;

/// <summary>
/// Atomic Redis scripts for fixed and segmented sliding windows. Redis server time is used,
/// so counters remain correct when application-node clocks drift.
/// </summary>
public sealed class RedisRateLimitStore(
    IConnectionMultiplexer connection,
    IOptions<RedisOptions> options) : IRedisRateLimitStore
{
    private const string FixedWindowScript = """
        local permits = tonumber(ARGV[1])
        local limit = tonumber(ARGV[2])
        local window = tonumber(ARGV[3])
        local current = tonumber(redis.call('GET', KEYS[1]) or '0')
        local acquired = 0

        if (permits == 0 and current < limit) or (permits > 0 and current + permits <= limit) then
          acquired = 1
          if permits > 0 then
            current = redis.call('INCRBY', KEYS[1], permits)
            if current == permits then
              redis.call('PEXPIRE', KEYS[1], window)
            end
          end
        end

        local ttl = redis.call('PTTL', KEYS[1])
        if ttl < 1 then ttl = window end
        return { acquired, math.max(0, limit - current), acquired == 1 and 0 or ttl }
        """;

    private const string SlidingWindowScript = """
        local permits = tonumber(ARGV[1])
        local limit = tonumber(ARGV[2])
        local window = tonumber(ARGV[3])
        local segments = tonumber(ARGV[4])
        local segmentLength = math.ceil(window / segments)
        local serverTime = redis.call('TIME')
        local now = (tonumber(serverTime[1]) * 1000) + math.floor(tonumber(serverTime[2]) / 1000)
        local currentSegment = math.floor(now / segmentLength)
        local minimumSegment = currentSegment - segments + 1
        local values = redis.call('HGETALL', KEYS[1])
        local total = 0
        local oldestSegment = currentSegment

        for index = 1, #values, 2 do
          local segment = tonumber(values[index])
          local count = tonumber(values[index + 1])
          if segment < minimumSegment then
            redis.call('HDEL', KEYS[1], values[index])
          else
            total = total + count
            if segment < oldestSegment then oldestSegment = segment end
          end
        end

        local acquired = 0
        if (permits == 0 and total < limit) or (permits > 0 and total + permits <= limit) then
          acquired = 1
          if permits > 0 then
            redis.call('HINCRBY', KEYS[1], tostring(currentSegment), permits)
            total = total + permits
          end
        end

        redis.call('PEXPIRE', KEYS[1], window + segmentLength)
        local retry = 0
        if acquired == 0 then
          retry = math.max(1, ((oldestSegment + segments) * segmentLength) - now)
        end
        return { acquired, math.max(0, limit - total), retry }
        """;

    private readonly IDatabase _database = connection.GetDatabase();
    private readonly string _instanceName = options.Value.InstanceName;

    public async ValueTask<RedisRateLimitDecision> AcquireFixedWindowAsync(
        string key,
        int permitLimit,
        int permitCount,
        TimeSpan window,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var result = await _database.ScriptEvaluateAsync(
            FixedWindowScript,
            [Key(key)],
            [permitCount, permitLimit, Milliseconds(window)]);
        return Decision(result);
    }

    public async ValueTask<RedisRateLimitDecision> AcquireSlidingWindowAsync(
        string key,
        int permitLimit,
        int permitCount,
        TimeSpan window,
        int segmentsPerWindow,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var result = await _database.ScriptEvaluateAsync(
            SlidingWindowScript,
            [Key(key)],
            [permitCount, permitLimit, Milliseconds(window), segmentsPerWindow]);
        return Decision(result);
    }

    private RedisKey Key(string key) => _instanceName + "ratelimit:" + key;

    private static long Milliseconds(TimeSpan value) =>
        Math.Max(1, checked((long)Math.Ceiling(value.TotalMilliseconds)));

    private static RedisRateLimitDecision Decision(RedisResult result)
    {
        var values = (RedisResult[])result!;
        var acquired = (long)values[0] == 1;
        var remaining = (long)values[1];
        var retryMilliseconds = (long)values[2];

        return new RedisRateLimitDecision(
            acquired,
            remaining,
            TimeSpan.FromMilliseconds(Math.Max(0, retryMilliseconds)));
    }
}
