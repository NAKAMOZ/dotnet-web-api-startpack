using System.Diagnostics;
using System.Threading.RateLimiting;

namespace Api.Services.RateLimiting;

/// <summary>Adapts Redis's atomic window decision to ASP.NET Core's limiter contract.</summary>
internal sealed class RedisWindowRateLimiter(
    IRedisRateLimitStore store,
    string key,
    int permitLimit,
    TimeSpan window,
    int segmentsPerWindow) : RateLimiter
{
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

    protected override RateLimitLease AttemptAcquireCore(int permitCount) =>
        AcquireCoreAsync(permitCount, CancellationToken.None)
            .AsTask()
            .GetAwaiter()
            .GetResult();

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
