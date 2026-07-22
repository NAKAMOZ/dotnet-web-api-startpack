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
        // TODO §7: AppDbContext with the Npgsql provider (ADR-0008); connection string
        //          from configuration; IEntityTypeConfiguration<T> discovery.
        // TODO §8: migration and seeding hooks.
        return services;
    }
}
