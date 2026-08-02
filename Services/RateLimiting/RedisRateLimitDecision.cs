namespace Api.Services.RateLimiting;

public readonly record struct RedisRateLimitDecision(
    bool IsAcquired,
    long RemainingPermits,
    TimeSpan RetryAfter);
