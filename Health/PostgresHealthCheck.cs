using Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Api.Health;

/// <summary>
/// Readiness probe that requires a fully applied migration chain. Connectivity is checked
/// independently by AspNetCore.HealthChecks.NpgSql under the same readiness tag.
/// </summary>
public sealed class PostgresHealthCheck(IServiceScopeFactory scopeFactory) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        try
        {
            var pendingMigrations = await database.Database
                .GetPendingMigrationsAsync(cancellationToken);

            return pendingMigrations.Any()
                ? HealthCheckResult.Unhealthy("PostgreSQL has pending migrations.")
                : HealthCheckResult.Healthy();
        }
        catch (Exception exception)
        {
            return HealthCheckResult.Unhealthy(
                "PostgreSQL is unavailable.",
                exception);
        }
    }
}
