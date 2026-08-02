using Microsoft.Extensions.Diagnostics.HealthChecks;
using StackExchange.Redis;

namespace Api.Health;

/// <summary>Readiness check for distributed cache and rate-limit state.</summary>
public sealed class RedisHealthCheck(IConnectionMultiplexer connection) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            await connection.GetDatabase().PingAsync();
            return HealthCheckResult.Healthy();
        }
        catch (Exception exception)
        {
            return HealthCheckResult.Unhealthy(
                "Redis distributed state is unavailable.",
                exception);
        }
    }
}
