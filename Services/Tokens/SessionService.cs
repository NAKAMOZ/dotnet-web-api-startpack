using Api.Configuration;
using Api.Data;
using Api.Models;
using Api.Models.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Api.Services.Tokens;

/// <inheritdoc cref="ISessionService"/>
public sealed class SessionService(
    AppDbContext dbContext,
    IOptions<AuthSessionOptions> sessionOptions,
    TimeProvider timeProvider) : ISessionService
{
    private readonly AuthSessionOptions _options = sessionOptions.Value;

    public async Task<Guid> CreateAsync(NewSessionRequest request, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();

        var securityStamp = await dbContext.Users
            .Where(user => user.Id == request.UserId)
            .Select(user => user.SecurityStamp)
            .SingleAsync(cancellationToken);

        var session = new Session
        {
            UserId = request.UserId,
            IpAddress = request.IpAddress,
            UserAgent = Truncate(request.UserAgent, 512),
            DeviceLabel = request.DeviceLabel,
            AuthenticationMethods = [.. request.AuthenticationMethods],

            // Captured at login. Refresh compares the user's current stamp against this one.
            // Password reset invalidates every snapshot; a deliberate password change
            // updates only the current session's snapshot and revokes its siblings.
            SecurityStamp = securityStamp,

            AuthenticatedAt = now,
            LastActiveAt = now,

            // Written once, here, and never touched again. Sliding this on refresh is the
            // single change that silently removes the 7-day ceiling (ADR-0002).
            AbsoluteExpiresAt = now + _options.AbsoluteLifetime,
        };

        dbContext.Sessions.Add(session);
        await dbContext.SaveChangesAsync(cancellationToken);

        return session.Id;
    }

    public async Task TouchAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        // Note what is NOT here: AbsoluteExpiresAt. A refresh slides the inactivity window
        // and nothing else.
        await dbContext.Sessions
            .Where(session => session.Id == sessionId)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(session => session.LastActiveAt, timeProvider.GetUtcNow()),
                cancellationToken);
    }

    public async Task MarkReauthenticatedAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        // Called only from a completed authentication flow. Calling it from TouchAsync or
        // from the refresh path would make every rotation look like a re-authentication and
        // defeat step-up entirely (Authentication.md §14).
        var now = timeProvider.GetUtcNow();

        await dbContext.Sessions
            .Where(session => session.Id == sessionId)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(session => session.AuthenticatedAt, now)
                    .SetProperty(session => session.LastActiveAt, now),
                cancellationToken);
    }

    public async Task RevokeAsync(
        Guid sessionId,
        SessionRevocationReason reason,
        CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();

        // Only live sessions: a revoked session must keep its original reason and timestamp,
        // or the audit trail records the last thing that touched it rather than what ended it.
        await dbContext.Sessions
            .Where(session => session.Id == sessionId && session.RevokedAt == null)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(session => session.RevokedAt, now)
                    .SetProperty(session => session.RevocationReason, reason),
                cancellationToken);
    }

    public async Task<int> RevokeAllForUserAsync(
        Guid userId,
        Guid? exceptSessionId,
        SessionRevocationReason reason,
        CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();

        var affected = await dbContext.Sessions
            .Where(session => session.UserId == userId
                              && session.RevokedAt == null
                              && (exceptSessionId == null || session.Id != exceptSessionId))
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(session => session.RevokedAt, now)
                    .SetProperty(session => session.RevocationReason, reason),
                cancellationToken);

        return affected;
    }

    private static string? Truncate(string? value, int maxLength) =>
        value is null || value.Length <= maxLength ? value : value[..maxLength];
}
