using Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Api.Extensions;

public static partial class ServiceCollectionExtensions
{
    /// <summary>
    /// EF Core, the PostgreSQL provider, and data-access services.
    /// </summary>
    public static IServiceCollection AddDataServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Postgres");

        // Fail at boot, not at the first request. A missing connection string that surfaces
        // as a 500 on the first login reads like a database outage; here it names itself.
        // §25 extends this to full startup validation of the options classes.
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "Connection string 'Postgres' is not configured. Set ConnectionStrings:Postgres " +
                "in configuration, or the ConnectionStrings__Postgres environment variable.");
        }

        services.TryAddSingleton(TimeProvider.System);

        // Stateless — it reads the clock through TimeProvider and touches nothing else — so
        // a singleton, shared by every scoped DbContext.
        services.TryAddSingleton<AuditableEntityInterceptor>();

        services.AddDbContext<AppDbContext>((serviceProvider, options) =>
        {
            options.UseNpgsql(connectionString, npgsql =>
            {
                // Transient faults — a failover, a dropped connection, a restarting
                // container — are retried rather than surfaced as a 500.
                //
                // The cost is real and worth stating up front: with a retrying execution
                // strategy, code that opens its own transaction must run through
                // db.Database.CreateExecutionStrategy().ExecuteAsync(...) or EF throws at
                // runtime. §12's refresh rotation is exactly that shape — DataAccess.md
                // carries the pattern.
                npgsql.EnableRetryOnFailure(
                    maxRetryCount: 3,
                    maxRetryDelay: TimeSpan.FromSeconds(5),
                    errorCodesToAdd: null);
            });

            options.AddInterceptors(serviceProvider.GetRequiredService<AuditableEntityInterceptor>());
        });

        // TODO §8: migration and seeding hooks.
        return services;
    }
}
