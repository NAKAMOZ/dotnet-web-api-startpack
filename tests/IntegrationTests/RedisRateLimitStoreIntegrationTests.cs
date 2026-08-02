using Api.Configuration;
using Api.Services.RateLimiting;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using Testcontainers.Redis;

namespace IntegrationTests;

public sealed class RedisRateLimitStoreIntegrationTests : IAsyncLifetime
{
    private readonly RedisContainer _redis = new RedisBuilder("redis:8.2-alpine").Build();
    private readonly string _instanceName = $"tests:{Guid.NewGuid():N}:";
    private IConnectionMultiplexer? _connection;

    public async ValueTask InitializeAsync()
    {
        await _redis.StartAsync();
        _connection = await ConnectionMultiplexer.ConnectAsync(_redis.GetConnectionString());
    }

    public async ValueTask DisposeAsync()
    {
        if (_connection is not null)
        {
            await _connection.CloseAsync();
            _connection.Dispose();
        }

        await _redis.DisposeAsync();
    }

    [Fact]
    public async Task FixedWindow_ConcurrentCallersShareOneAtomicLimit()
    {
        var store = CreateStore();
        var attempts = Enumerable.Range(0, 100)
            .Select(_ => store.AcquireFixedWindowAsync(
                    "fixed-shared",
                    permitLimit: 10,
                    permitCount: 1,
                    TimeSpan.FromMinutes(1),
                    TestContext.Current.CancellationToken)
                .AsTask());

        var decisions = await Task.WhenAll(attempts);

        Assert.Equal(10, decisions.Count(decision => decision.IsAcquired));
        Assert.All(
            decisions.Where(decision => !decision.IsAcquired),
            decision => Assert.True(decision.RetryAfter > TimeSpan.Zero));
    }

    [Fact]
    public async Task SlidingWindow_SeparateStoreInstancesCannotExceedSharedLimit()
    {
        var firstNode = CreateStore();
        var secondNode = CreateStore();
        var attempts = Enumerable.Range(0, 40)
            .Select(index => (index % 2 == 0 ? firstNode : secondNode)
                .AcquireSlidingWindowAsync(
                    "sliding-shared",
                    permitLimit: 7,
                    permitCount: 1,
                    TimeSpan.FromMinutes(1),
                    segmentsPerWindow: 6,
                    TestContext.Current.CancellationToken)
                .AsTask());

        var decisions = await Task.WhenAll(attempts);

        Assert.Equal(7, decisions.Count(decision => decision.IsAcquired));
        Assert.Equal(0, decisions.Last().RemainingPermits);
    }

    private RedisRateLimitStore CreateStore() =>
        new(
            _connection ?? throw new InvalidOperationException("Redis has not started."),
            Options.Create(new RedisOptions
            {
                Enabled = true,
                Endpoint = _redis.GetConnectionString(),
                InstanceName = _instanceName,
            }));
}
