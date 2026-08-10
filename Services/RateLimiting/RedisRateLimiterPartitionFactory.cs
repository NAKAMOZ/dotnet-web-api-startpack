using System.Security.Cryptography;
using System.Text;
using System.Threading.RateLimiting;
using Api.Configuration;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace Api.Services.RateLimiting;

/// <summary>Creates Redis-backed limiters while keeping raw IPs and subjects out of keys.</summary>
public sealed class RedisRateLimiterPartitionFactory(
    IRedisRateLimitStore store,
    IOptions<RedisOptions> options) : IRateLimiterPartitionFactory
{
    private readonly string _keyPrefix = options.Value.InstanceName + "ratelimit:";

    public RateLimiter CreateFixedWindow(
        string policy,
        string partitionKey,
        int permitLimit,
        TimeSpan window) =>
        new RedisWindowRateLimiter(
            store,
            Key(policy, partitionKey),
            permitLimit,
            window,
            segmentsPerWindow: 1);

    public RateLimiter CreateSlidingWindow(
        string policy,
        string partitionKey,
        int permitLimit,
        TimeSpan window,
        int segmentsPerWindow) =>
        new RedisWindowRateLimiter(
            store,
            Key(policy, partitionKey),
            permitLimit,
            window,
            segmentsPerWindow);

    /// <summary>
    /// The complete Redis key, built once per partition. The limiter this returns is cached by
    /// <c>PartitionedRateLimiter</c> for the life of the partition, so composing the key here
    /// rather than inside the store takes the instance prefix and the hex digest off the
    /// per-request path entirely.
    /// </summary>
    private RedisKey Key(string policy, string partitionKey)
    {
        var digest = SHA256.HashData(Encoding.UTF8.GetBytes(partitionKey));
        return _keyPrefix + policy + ":" + Convert.ToHexString(digest);
    }
}
