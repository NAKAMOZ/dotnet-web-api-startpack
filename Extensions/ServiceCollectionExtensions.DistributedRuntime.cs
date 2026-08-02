using Api.Configuration;
using Api.Services.RateLimiting;
using Azure.Core;
using Azure.Identity;
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
        var redis = configuration
            .GetSection(RedisOptions.SectionName)
            .Get<RedisOptions>() ?? new RedisOptions();

        if (!redis.Enabled)
        {
            services.AddSingleton<IRateLimiterPartitionFactory, LocalRateLimiterPartitionFactory>();
            services.AddHybridCache();
            return services;
        }

        services.AddSingleton<IConnectionMultiplexer>(provider =>
            CreateRedisConnection(
                redis,
                configuration
                    .GetSection(AzurePlatformOptions.SectionName)
                    .Get<AzurePlatformOptions>() ?? new AzurePlatformOptions(),
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
            TokenCredential credential = string.IsNullOrWhiteSpace(azure.ManagedIdentityClientId)
                ? new ManagedIdentityCredential(ManagedIdentityId.SystemAssigned)
                : new ManagedIdentityCredential(
                    ManagedIdentityId.FromUserAssignedClientId(azure.ManagedIdentityClientId));
            configuration
                .ConfigureForAzureWithTokenCredentialAsync(credential)
                .GetAwaiter()
                .GetResult();
        }

        return ConnectionMultiplexer.Connect(configuration);
    }
}
