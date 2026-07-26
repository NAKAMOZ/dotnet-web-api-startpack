using Api.Data;
using Api.Logging;
using Microsoft.EntityFrameworkCore;

namespace Api.Services.Monitoring;

/// <summary>
/// Keeps the process-local active-session gauge in step with PostgreSQL.
/// </summary>
/// <remarks>
/// <para>
/// This is the only writer of <c>auth.active_sessions</c>. Session mutations deliberately
/// do not update it: <c>ObservableGauge</c> is a pull instrument, so counting on every
/// login and revocation makes the cost scale with write traffic rather than with the
/// scrape interval, and it puts a <c>COUNT</c> over live sessions on the login and refresh
/// hot paths for the sake of a dashboard number.
/// </para>
/// <para>
/// Sampling on a timer instead also covers the case no mutation hook can: a session that
/// simply reaches <c>AbsoluteExpiresAt</c> changes no row, so a push-maintained gauge only
/// ever drifts upward. It means no future session-mutating path — cleanup, mass revocation,
/// admin delete — carries a duty to remember this metric.
/// </para>
/// </remarks>
public sealed class ActiveSessionMetricsCollector(
    IServiceScopeFactory scopeFactory,
    TimeProvider timeProvider,
    AuthMetrics metrics,
    ILogger<ActiveSessionMetricsCollector> logger) : BackgroundService
{
    /// <summary>
    /// Traffic-independent. Well under any realistic scrape interval, and one query a
    /// minute is immaterial next to the per-mutation counting it replaces.
    /// </summary>
    private static readonly TimeSpan SampleInterval = TimeSpan.FromMinutes(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Keep host startup non-blocking. Readiness, not this dashboard sample, owns whether
        // the instance receives traffic.
        await Task.Yield();

        using var timer = new PeriodicTimer(SampleInterval);

        try
        {
            do
            {
                await SampleAsync(stoppingToken);
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Normal shutdown.
        }
    }

    private async Task SampleAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var database = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var now = timeProvider.GetUtcNow();
            var count = await database.Sessions
                .AsNoTracking()
                .CountAsync(
                    session => session.RevokedAt == null && session.AbsoluteExpiresAt > now,
                    cancellationToken);

            metrics.SetActiveSessions(count);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            // Readiness separately reports the database failure, and the next tick retries.
            // Metrics collection must not convert an observable dependency outage into a
            // process crash — nor into a permanently dead sampler.
            logger.LogWarning(
                exception,
                "Could not refresh the active-session metric from PostgreSQL.");
        }
    }
}
