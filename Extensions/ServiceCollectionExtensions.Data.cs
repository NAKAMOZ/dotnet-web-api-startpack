using Api.Data;
using Api.Data.Seeding;
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
        // Fail at boot, not at the first request. §25 extends this to full startup validation
        // of the options classes.
        var connectionString = AppDbContext.ResolveConnectionString(configuration);

        services.TryAddSingleton(TimeProvider.System);

        // Stateless — it reads the clock through TimeProvider and touches nothing else — so
        // a singleton, shared by every scoped DbContext.
        services.TryAddSingleton<AuditableEntityInterceptor>();

        services.AddDbContext<AppDbContext>((serviceProvider, options) =>
        {
            options.UseNpgsql(connectionString, AppDbContext.ConfigureNpgsql);

            options.AddInterceptors(serviceProvider.GetRequiredService<AuditableEntityInterceptor>());
        });

        // Registered unconditionally, gated at both call sites: UseDatabaseSetupAsync only
        // invokes it in Development, and the seeder itself re-checks the environment. A
        // registration-time environment check would leave the second guard resolving
        // nothing, which is a worse failure than one that refuses out loud.
        services.AddScoped<IDataSeeder, DevDataSeeder>();

        return services;
    }
}
