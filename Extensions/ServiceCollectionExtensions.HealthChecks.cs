using Api.Configuration;
using Api.Health;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Api.Extensions;

public static partial class ServiceCollectionExtensions
{
    /// <summary>Registers dependency-free liveness and dependency-aware readiness (§28).</summary>
    public static IServiceCollection AddApplicationHealthChecks(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Postgres")
            ?? throw new InvalidOperationException("Connection string 'Postgres' is not configured.");

        // Singleton so the migrations check can remember its answer between probes.
        services.AddSingleton<PostgresHealthCheck>();

        var checks = services
            .AddHealthChecks()
            .AddCheck(
                "self",
                () => HealthCheckResult.Healthy(),
                tags: ["live"])
            .AddNpgSql(
                connectionString,
                name: "postgres",
                failureStatus: HealthStatus.Unhealthy,
                tags: ["ready"],
                timeout: TimeSpan.FromSeconds(5))
            .AddCheck<PostgresHealthCheck>(
                "migrations",
                failureStatus: HealthStatus.Unhealthy,
                tags: ["ready"],
                timeout: TimeSpan.FromSeconds(5));

        var redis = configuration
            .GetSection(RedisOptions.SectionName)
            .Get<RedisOptions>() ?? new RedisOptions();
        if (redis.Enabled)
        {
            checks.AddCheck<RedisHealthCheck>(
                "redis",
                failureStatus: HealthStatus.Unhealthy,
                tags: ["ready"],
                timeout: TimeSpan.FromSeconds(5));
        }

        return services;
    }
}
