using System.Security.Claims;
using Api.Configuration;
using Api.Handlers.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace UnitTests.Authorization;

public class RecentAuthAuthorizationHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 22, 12, 0, 0, TimeSpan.Zero);

    private static async Task<bool> EvaluateAsync(params Claim[] claims)
    {
        var timeProvider = new FakeTimeProvider(Now);
        var options = new AuthSessionOptions { RecentAuthenticationWindow = TimeSpan.FromMinutes(5) };
        var handler = new RecentAuthAuthorizationHandler(
            timeProvider,
            new StaticOptionsMonitor<AuthSessionOptions>(options));

        var user = new ClaimsPrincipal(new ClaimsIdentity(claims, authenticationType: "Test"));
        var context = new AuthorizationHandlerContext([new RecentAuthRequirement()], user, resource: null);

        await handler.HandleAsync(context);
        return context.HasSucceeded;
    }

    private static Claim AuthTime(DateTimeOffset at) =>
        new(RecentAuthAuthorizationHandler.AuthTimeClaimType, at.ToUnixTimeSeconds().ToString());

    [Fact]
    public async Task SucceedsInsideTheWindow()
    {
        Assert.True(await EvaluateAsync(AuthTime(Now.AddMinutes(-4))));
    }

    [Fact]
    public async Task FailsOutsideTheWindow()
    {
        Assert.False(await EvaluateAsync(AuthTime(Now.AddMinutes(-6))));
    }

    [Fact]
    public async Task FailsExactlyAtTheBoundary()
    {
        // Boundary is exclusive: 5 minutes old is no longer "recent".
        Assert.False(await EvaluateAsync(AuthTime(Now.AddMinutes(-5))));
    }

    [Fact]
    public async Task FailsWhenAuthTimeIsAbsent()
    {
        // API keys land here — no human authenticated, so step-up can never be satisfied.
        Assert.False(await EvaluateAsync(new Claim(ClaimTypes.NameIdentifier, "user-1")));
    }

    [Fact]
    public async Task FailsWhenAuthTimeIsInTheFuture()
    {
        // Clock problem or a forged claim. Must read as "not recent", never as "very recent".
        Assert.False(await EvaluateAsync(AuthTime(Now.AddMinutes(10))));
    }

    [Fact]
    public async Task FailsWhenAuthTimeIsNotANumber()
    {
        Assert.False(await EvaluateAsync(
            new Claim(RecentAuthAuthorizationHandler.AuthTimeClaimType, "yesterday")));
    }

    private sealed class StaticOptionsMonitor<T>(T value) : IOptionsMonitor<T>
    {
        public T CurrentValue { get; } = value;

        public T Get(string? name) => CurrentValue;

        public IDisposable? OnChange(Action<T, string?> listener) => null;
    }
}
