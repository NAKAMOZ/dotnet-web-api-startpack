using Api.Data;
using Api.Models.Enums;
using Api.Services.Tokens;
using IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace IntegrationTests;

[Collection(IntegrationTestCollection.Name)]
public sealed class TokenServiceIntegrationTests(IntegrationTestFactory factory)
{
    [Fact]
    public async Task RefreshRotation_ReplayRevokesSessionAndAuditsBothEvents()
    {
        await factory.ResetAsync();
        var seeded = await SeedSessionAndTokenAsync();

        var rotated = await factory.InScopeAsync(services =>
            services.GetRequiredService<IRefreshTokenService>().RotateAsync(
                seeded.RefreshToken,
                TestContext.Current.CancellationToken));

        Assert.Equal(RefreshOutcome.Rotated, rotated.Outcome);
        Assert.NotNull(rotated.AccessToken);
        Assert.NotNull(rotated.RefreshToken);

        var replayed = await factory.InScopeAsync(services =>
            services.GetRequiredService<IRefreshTokenService>().RotateAsync(
                seeded.RefreshToken,
                TestContext.Current.CancellationToken));

        Assert.Equal(RefreshOutcome.ReuseDetected, replayed.Outcome);

        var legitimateHolder = await factory.InScopeAsync(services =>
            services.GetRequiredService<IRefreshTokenService>().RotateAsync(
                rotated.RefreshToken!.Value,
                TestContext.Current.CancellationToken));

        Assert.Equal(RefreshOutcome.SessionRevoked, legitimateHolder.Outcome);

        await factory.InScopeAsync(async services =>
        {
            var database = services.GetRequiredService<AppDbContext>();
            var session = await database.Sessions
                .SingleAsync(candidate => candidate.Id == seeded.SessionId);
            var events = await database.AuditLogEntries
                .Where(entry => entry.UserId == seeded.UserId)
                .OrderBy(entry => entry.OccurredAt)
                .Select(entry => entry.EventType)
                .ToListAsync(TestContext.Current.CancellationToken);

            Assert.Equal(SessionRevocationReason.TokenReuseDetected, session.RevocationReason);
            Assert.Equal(2, events.Count);
            Assert.Contains(AuditEventType.TokenRefreshed, events);
            Assert.Contains(AuditEventType.TokenReuseDetected, events);
        });
    }

    [Fact]
    public async Task RefreshRotation_EnforcesIdleAndAbsoluteSessionBounds()
    {
        await factory.ResetAsync();
        var idleSession = await SeedSessionAndTokenAsync();

        factory.Clock.Advance(TimeSpan.FromHours(6));

        var idle = await factory.InScopeAsync(services =>
            services.GetRequiredService<IRefreshTokenService>().RotateAsync(
                idleSession.RefreshToken,
                TestContext.Current.CancellationToken));

        Assert.Equal(RefreshOutcome.SessionIdle, idle.Outcome);

        await factory.ResetAsync();
        var expiredSession = await SeedSessionAndTokenAsync();

        factory.Clock.Advance(TimeSpan.FromDays(7));

        var expired = await factory.InScopeAsync(services =>
            services.GetRequiredService<IRefreshTokenService>().RotateAsync(
                expiredSession.RefreshToken,
                TestContext.Current.CancellationToken));

        Assert.Equal(RefreshOutcome.TokenExpired, expired.Outcome);
    }

    private async Task<SeededSession> SeedSessionAndTokenAsync()
    {
        var userId = await factory.SeedUserAsync(TestContext.Current.CancellationToken);

        var sessionId = await factory.InScopeAsync(services =>
            services.GetRequiredService<ISessionService>().CreateAsync(
                new NewSessionRequest
                {
                    UserId = userId,
                    AuthenticationMethods = [AuthenticationMethod.Password],
                    IpAddress = "192.0.2.10",
                    UserAgent = "IntegrationTests/1.0",
                    DeviceLabel = "test",
                },
                TestContext.Current.CancellationToken));

        var refresh = await factory.InScopeAsync(services =>
            services.GetRequiredService<IRefreshTokenService>().IssueAsync(
                sessionId,
                TestContext.Current.CancellationToken));

        return new SeededSession(userId, sessionId, refresh.Value);
    }

    private sealed record SeededSession(Guid UserId, Guid SessionId, string RefreshToken);
}
