using Api.Data;
using Api.DTOs.Sessions;
using Api.Exceptions;
using Api.Models.Enums;
using Api.Services.Audit;
using Api.Services.Tokens;
using Microsoft.EntityFrameworkCore;

namespace Api.Services.Sessions;

public sealed class SessionQueryService(
    AppDbContext dbContext,
    ISessionService sessionService,
    IRefreshTokenService refreshTokenService,
    IAuditLogger auditLogger,
    TimeProvider timeProvider) : ISessionQueryService
{
    public async Task<IReadOnlyList<SessionResponse>> ListAsync(
        Guid userId,
        Guid currentSessionId,
        CancellationToken cancellationToken) =>
        await dbContext.Sessions
            .AsNoTracking()
            .Where(session => session.UserId == userId && session.RevokedAt == null)
            .OrderByDescending(session => session.LastActiveAt)
            .Select(session => new SessionResponse
            {
                Id = session.Id,
                DeviceLabel = session.DeviceLabel,
                IpAddress = session.IpAddress,
                CreatedAt = session.CreatedAt,
                LastActiveAt = session.LastActiveAt,
                AbsoluteExpiresAt = session.AbsoluteExpiresAt,
                IsCurrent = session.Id == currentSessionId,
            })
            .ToListAsync(cancellationToken);

    public async Task RevokeAsync(
        Guid userId,
        Guid sessionId,
        CancellationToken cancellationToken)
    {
        var exists = await dbContext.Sessions.AnyAsync(
            session => session.Id == sessionId
                       && session.UserId == userId
                       && session.RevokedAt == null,
            cancellationToken);

        if (!exists)
        {
            throw new ResourceNotFoundException("session");
        }

        await sessionService.RevokeAsync(
            sessionId,
            SessionRevocationReason.UserRevokedSession,
            cancellationToken);
        await refreshTokenService.RevokeForSessionAsync(sessionId, cancellationToken);
        await auditLogger.LogAsync(
            AuditEventType.SessionRevoked,
            userId,
            new { SessionId = sessionId, Reason = SessionRevocationReason.UserRevokedSession },
            cancellationToken);
    }

    public async Task<RevokeSessionsResponse> RevokeAllOthersAsync(
        Guid userId,
        Guid currentSessionId,
        CancellationToken cancellationToken)
    {
        var sessionIds = await dbContext.Sessions
            .Where(session => session.UserId == userId
                              && session.Id != currentSessionId
                              && session.RevokedAt == null)
            .Select(session => session.Id)
            .ToListAsync(cancellationToken);
        var count = await sessionService.RevokeAllForUserAsync(
            userId,
            currentSessionId,
            SessionRevocationReason.UserRevokedAllSessions,
            cancellationToken);

        if (sessionIds.Count > 0)
        {
            var now = timeProvider.GetUtcNow();
            await dbContext.RefreshTokens
                .Where(token => sessionIds.Contains(token.SessionId)
                                && token.UsedAt == null
                                && token.ExpiresAt > now)
                .ExecuteUpdateAsync(
                    setters => setters.SetProperty(token => token.ExpiresAt, now),
                    cancellationToken);
            await auditLogger.LogAsync(
                AuditEventType.SessionRevoked,
                userId,
                new { Reason = SessionRevocationReason.UserRevokedAllSessions, Count = count },
                cancellationToken);
        }

        return new RevokeSessionsResponse { RevokedCount = count };
    }
}
