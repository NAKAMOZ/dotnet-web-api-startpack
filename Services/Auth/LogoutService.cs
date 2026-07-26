using Api.Models.Enums;
using Api.Services.Audit;
using Api.Services.Tokens;

namespace Api.Services.Auth;

public sealed class LogoutService(
    ISessionService sessionService,
    IRefreshTokenService refreshTokenService,
    IAuthTokenTransport transport,
    IAuditLogger auditLogger) : ILogoutService
{
    public async Task LogoutAsync(
        Guid userId,
        Guid sessionId,
        CancellationToken cancellationToken)
    {
        await sessionService.RevokeAsync(sessionId, SessionRevocationReason.Logout, cancellationToken);
        await refreshTokenService.RevokeForSessionAsync(sessionId, cancellationToken);
        transport.ClearCookies();
        await auditLogger.LogAsync(
            AuditEventType.SessionRevoked,
            userId,
            new { SessionId = sessionId, Reason = SessionRevocationReason.Logout },
            cancellationToken);
    }
}
