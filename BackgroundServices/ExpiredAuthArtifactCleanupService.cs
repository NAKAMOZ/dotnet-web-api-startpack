using Api.Configuration;
using Api.Data;
using Api.Services.Tokens;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Api.BackgroundServices;

/// <summary>Periodically removes expired authentication artifacts in bounded batches.</summary>
public sealed class ExpiredAuthArtifactCleanupService(
    IServiceScopeFactory scopeFactory,
    IOptions<CleanupOptions> options,
    TimeProvider timeProvider,
    ILogger<ExpiredAuthArtifactCleanupService> logger) : BackgroundService
{
    private readonly CleanupOptions _options = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(_options.Interval, timeProvider);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await CleanupOnceAsync(stoppingToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                logger.LogError(exception, "Expired authentication artifact cleanup failed.");
            }
        }
    }

    internal async Task CleanupOnceAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var signingKeys = scope.ServiceProvider.GetRequiredService<ISigningKeyManager>();
        var now = timeProvider.GetUtcNow();

        await DeleteRefreshTokensAsync(database, now, cancellationToken);
        await DeleteSessionsAsync(database, now, cancellationToken);
        await DeleteVerificationTokensAsync(database, now, cancellationToken);
        await DeleteAuditEntriesAsync(database, now - _options.AuditRetention, cancellationToken);
        await signingKeys.RetireElapsedKeysAsync(cancellationToken);
    }

    private async Task DeleteRefreshTokensAsync(
        AppDbContext database,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var ids = await database.RefreshTokens
            // Spent tokens remain until their session's absolute expiry. Deleting them
            // immediately would erase the evidence needed to detect a replay.
            .Where(token => token.ExpiresAt <= now)
            .OrderBy(token => token.ExpiresAt)
            .Select(token => token.Id)
            .Take(_options.BatchSize)
            .ToListAsync(cancellationToken);

        if (ids.Count > 0)
        {
            await database.RefreshTokens
                .Where(token => ids.Contains(token.Id))
                .ExecuteDeleteAsync(cancellationToken);
        }
    }

    private async Task DeleteSessionsAsync(
        AppDbContext database,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var ids = await database.Sessions
            .Where(session => session.AbsoluteExpiresAt <= now)
            .OrderBy(session => session.AbsoluteExpiresAt)
            .Select(session => session.Id)
            .Take(_options.BatchSize)
            .ToListAsync(cancellationToken);

        if (ids.Count > 0)
        {
            await database.Sessions
                .Where(session => ids.Contains(session.Id))
                .ExecuteDeleteAsync(cancellationToken);
        }
    }

    private async Task DeleteVerificationTokensAsync(
        AppDbContext database,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var ids = await database.VerificationTokens
            .Where(token => token.ExpiresAt <= now || token.ConsumedAt != null)
            .OrderBy(token => token.ExpiresAt)
            .Select(token => token.Id)
            .Take(_options.BatchSize)
            .ToListAsync(cancellationToken);

        if (ids.Count > 0)
        {
            await database.VerificationTokens
                .Where(token => ids.Contains(token.Id))
                .ExecuteDeleteAsync(cancellationToken);
        }
    }

    private async Task DeleteAuditEntriesAsync(
        AppDbContext database,
        DateTimeOffset cutoff,
        CancellationToken cancellationToken)
    {
        var ids = await database.AuditLogEntries
            .Where(entry => entry.OccurredAt <= cutoff)
            .OrderBy(entry => entry.OccurredAt)
            .Select(entry => entry.Id)
            .Take(_options.BatchSize)
            .ToListAsync(cancellationToken);

        if (ids.Count > 0)
        {
            await database.AuditLogEntries
                .Where(entry => ids.Contains(entry.Id))
                .ExecuteDeleteAsync(cancellationToken);
        }
    }
}
