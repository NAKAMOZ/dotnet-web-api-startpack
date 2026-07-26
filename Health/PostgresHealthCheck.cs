using Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Api.Health;

/// <summary>
/// Readiness probe that requires a fully applied migration chain. Connectivity is checked
/// independently by AspNetCore.HealthChecks.NpgSql under the same readiness tag.
/// </summary>
/// <remarks>
/// Registered as a singleton so the confirmation below survives between probes, which is
/// also why it takes a scope factory rather than an <c>AppDbContext</c>: the context is
/// scoped and this object outlives any one health-check run.
/// </remarks>
public sealed class PostgresHealthCheck(IServiceScopeFactory scopeFactory) : IHealthCheck
{
    // The pending set is a function of the loaded assembly and rows only MigrateAsync writes,
    // so it cannot change under a running process. Without this the probe re-reads the history
    // table every few seconds, forever, to reprint the same answer — and the sibling
    // connectivity check is what actually notices the database going away.
    private volatile bool _migrationsConfirmed;

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        if (_migrationsConfirmed)
        {
            return HealthCheckResult.Healthy();
        }

        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var database = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var pendingMigrations = await database.Database
                .GetPendingMigrationsAsync(cancellationToken);

            if (pendingMigrations.Any())
            {
                return HealthCheckResult.Unhealthy("PostgreSQL has pending migrations.");
            }

            _migrationsConfirmed = true;
            return HealthCheckResult.Healthy();
        }
        catch (Exception exception)
        {
            return HealthCheckResult.Unhealthy(
                "PostgreSQL is unavailable.",
                exception);
        }
    }
}
