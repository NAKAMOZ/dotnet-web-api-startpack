using Api.Configuration;
using Api.Services.RateLimiting;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using StackExchange.Redis;

namespace Api.Extensions;

public static partial class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the cache and rate-limit state store. Redis is optional for a local
    /// single-node process and mandatory in the horizontally-scaled Azure template.
    /// </summary>
    public static IServiceCollection AddDistributedRuntimeServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var redis = BindSection<RedisOptions>(configuration, RedisOptions.SectionName);

        if (!redis.Enabled)
        {
            services.AddSingleton<IRateLimiterPartitionFactory, LocalRateLimiterPartitionFactory>();
            services.AddHybridCache();
            return services;
        }

        // Bound here rather than inside the factory below: the lambda runs on first
        // resolution, and re-binding a section by reflection at that point would also capture
        // `configuration` for the lifetime of the singleton.
        var azure = BindSection<AzurePlatformOptions>(configuration, AzurePlatformOptions.SectionName);

        services.AddSingleton<IConnectionMultiplexer>(provider =>
            CreateRedisConnection(
                redis,
                azure,
                provider.GetRequiredService<ILoggerFactory>()));

        services.AddStackExchangeRedisCache(options => options.InstanceName = redis.InstanceName);
        services
            .AddOptions<RedisCacheOptions>()
            .Configure<IConnectionMultiplexer>((options, connection) =>
                options.ConnectionMultiplexerFactory = () => Task.FromResult(connection));

        services.AddHybridCache();
        services.AddSingleton<IRedisRateLimitStore, RedisRateLimitStore>();
        services.AddSingleton<IRateLimiterPartitionFactory, RedisRateLimiterPartitionFactory>();

        return services;
    }

    private static IConnectionMultiplexer CreateRedisConnection(
        RedisOptions redis,
        AzurePlatformOptions azure,
        ILoggerFactory loggerFactory)
    {
        var configuration = ConfigurationOptions.Parse(redis.Endpoint!, ignoreUnknown: false);
        configuration.AbortOnConnectFail = false;
        configuration.ConnectTimeout = redis.ConnectTimeoutMilliseconds;
        configuration.LoggerFactory = loggerFactory;

        if (redis.UseAzureIdentity)
        {
            configuration.Ssl = true;
            configuration
                .ConfigureForAzureWithTokenCredentialAsync(azure.CreateCredential())
                .GetAwaiter()
                .GetResult();
        }

        return ConnectionMultiplexer.Connect(configuration);
    }
}
