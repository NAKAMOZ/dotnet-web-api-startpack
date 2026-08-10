using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Api.Data;

/// <summary>
/// Creates the model for EF tooling without executing the HTTP composition root or touching
/// a database. Runtime contexts continue to come exclusively from dependency injection.
/// </summary>
public sealed class AppDbContextDesignTimeFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
                          ?? Environments.Development;
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile($"appsettings.{environment}.json", optional: true)
            .AddUserSecrets<AppDbContextDesignTimeFactory>(optional: true)
            .AddEnvironmentVariables()
            .AddCommandLine(args)
            .Build();
        // Both the connection string and the provider options come from AppDbContext, so a
        // migration is always scaffolded against the model the running host builds.
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(
                AppDbContext.ResolveConnectionString(configuration),
                AppDbContext.ConfigureNpgsql)
            .Options;

        return new AppDbContext(options);
    }
}
