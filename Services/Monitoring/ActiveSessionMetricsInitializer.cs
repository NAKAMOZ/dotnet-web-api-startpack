using Api.Data;
using Api.Logging;
using Microsoft.EntityFrameworkCore;

namespace Api.Services.Monitoring;

/// <summary>
/// Seeds the process-local active-session gauge from PostgreSQL after the host starts.
/// Session mutations keep it current afterwards.
/// </summary>
public sealed class ActiveSessionMetricsInitializer(
    IServiceScopeFactory scopeFactory,
    TimeProvider timeProvider,
    AuthMetrics metrics,
    ILogger<ActiveSessionMetricsInitializer> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Keep host startup non-blocking. Readiness, not this dashboard sample, owns whether
        // the instance receives traffic.
        await Task.Yield();

        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var database = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var now = timeProvider.GetUtcNow();
            var count = await database.Sessions
                .AsNoTracking()
                .CountAsync(
                    session => session.RevokedAt == null && session.AbsoluteExpiresAt > now,
                    stoppingToken);

            metrics.SetActiveSessions(count);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Normal shutdown while the one-time sample is in flight.
        }
        catch (Exception exception)
        {
            // Readiness separately reports the database failure. Metrics initialization must
            // not convert an observable dependency outage into a process crash.
            logger.LogWarning(
                exception,
                "Could not initialize the active-session metric from PostgreSQL.");
        }
    }
}
