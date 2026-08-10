using System.Diagnostics;
using System.Threading.RateLimiting;
using StackExchange.Redis;

namespace Api.Services.RateLimiting;

/// <summary>Adapts Redis's atomic window decision to ASP.NET Core's limiter contract.</summary>
internal sealed class RedisWindowRateLimiter(
    IRedisRateLimitStore store,
    RedisKey key,
    int permitLimit,
    TimeSpan window,
    int segmentsPerWindow) : RateLimiter
{
    private static readonly RateLimitLease NotAcquiredWithoutRedis =
        new RedisRateLimitLease(false, TimeSpan.Zero);

    private long _availablePermits = permitLimit;
    private long _failedLeases;
    private long _lastActivityTimestamp = Stopwatch.GetTimestamp();
    private long _successfulLeases;

    // PartitionedRateLimiter uses this to evict inactive per-IP/per-account wrapper
    // objects. The authoritative counter remains in Redis, so evicting a wrapper cannot
    // reset or bypass the window.
    public override TimeSpan? IdleDuration =>
        Stopwatch.GetElapsedTime(Interlocked.Read(ref _lastActivityTimestamp));

    public override RateLimiterStatistics GetStatistics() => new()
    {
        CurrentAvailablePermits = Math.Max(0, Interlocked.Read(ref _availablePermits)),
        CurrentQueuedCount = 0,
        TotalFailedLeases = Interlocked.Read(ref _failedLeases),
        TotalSuccessfulLeases = Interlocked.Read(ref _successfulLeases),
    };

    /// <summary>
    /// Declines without touching Redis. The caller then takes the asynchronous path, which is
    /// the only one that can reach the network without blocking.
    /// </summary>
    /// <remarks>
    /// <c>RateLimitingMiddleware</c> tries this synchronous attempt first and falls back to
    /// <see cref="AcquireAsyncCore"/> when it returns a lease that was not acquired, honouring
    /// whatever the async path decides. Answering it over the wire instead would block a
    /// thread-pool thread for a full Redis round trip on every request — the shape that turns
    /// a slow Redis into thread-pool starvation rather than slow responses.
    /// <para>
    /// Declining is free of side effects: both Lua scripts only increment on the branch that
    /// grants, so a refusal here consumes no permit and the counters stay exact.
    /// </para>
    /// </remarks>
    protected override RateLimitLease AttemptAcquireCore(int permitCount) =>
        NotAcquiredWithoutRedis;

    protected override ValueTask<RateLimitLease> AcquireAsyncCore(
        int permitCount,
        CancellationToken cancellationToken) =>
        AcquireCoreAsync(permitCount, cancellationToken);

    private async ValueTask<RateLimitLease> AcquireCoreAsync(
        int permitCount,
        CancellationToken cancellationToken)
    {
        var decision = segmentsPerWindow == 1
            ? await store.AcquireFixedWindowAsync(
                key,
                permitLimit,
                permitCount,
                window,
                cancellationToken)
            : await store.AcquireSlidingWindowAsync(
                key,
                permitLimit,
                permitCount,
                window,
                segmentsPerWindow,
                cancellationToken);

        Interlocked.Exchange(ref _availablePermits, decision.RemainingPermits);
        Interlocked.Exchange(ref _lastActivityTimestamp, Stopwatch.GetTimestamp());
        if (decision.IsAcquired)
        {
            Interlocked.Increment(ref _successfulLeases);
        }
        else
        {
            Interlocked.Increment(ref _failedLeases);
        }

        return new RedisRateLimitLease(decision.IsAcquired, decision.RetryAfter);
    }

    private sealed class RedisRateLimitLease(bool acquired, TimeSpan retryAfter) : RateLimitLease
    {
        private static readonly string[] RetryMetadataNames = [MetadataName.RetryAfter.Name];

        public override bool IsAcquired => acquired;

        public override IEnumerable<string> MetadataNames =>
            acquired ? [] : RetryMetadataNames;

        public override bool TryGetMetadata(string metadataName, out object? metadata)
        {
            if (!acquired && metadataName == MetadataName.RetryAfter.Name)
            {
                metadata = retryAfter;
                return true;
            }

            metadata = null;
            return false;
        }
    }
}
