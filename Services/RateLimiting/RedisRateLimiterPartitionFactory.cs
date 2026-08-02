using System.Security.Cryptography;
using System.Text;
using System.Threading.RateLimiting;

namespace Api.Services.RateLimiting;

/// <summary>Creates Redis-backed limiters while keeping raw IPs and subjects out of keys.</summary>
public sealed class RedisRateLimiterPartitionFactory(IRedisRateLimitStore store)
    : IRateLimiterPartitionFactory
{
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

    private static string Key(string policy, string partitionKey)
    {
        var digest = SHA256.HashData(Encoding.UTF8.GetBytes(partitionKey));
        return policy + ":" + Convert.ToHexString(digest);
    }
}
