using Api.Data;
using Api.DTOs.Auth;
using Api.DTOs.Common;
using Api.Models.Enums;
using Api.Services.Tokens;
using Microsoft.EntityFrameworkCore;

namespace Api.Services.Auth;

public sealed class AuthenticationSessionFactory(
    AppDbContext dbContext,
    ISessionService sessionService,
    IAccessTokenIssuer accessTokenIssuer,
    IRefreshTokenService refreshTokenService,
    IAuthTokenTransport transport,
    IHttpContextAccessor httpContextAccessor,
    TimeProvider timeProvider) : IAuthenticationSessionFactory
{
    public async Task<LoginResponse> CreateAsync(
        Guid userId,
        IReadOnlyCollection<AuthenticationMethod> methods,
        CancellationToken cancellationToken)
    {
        var user = await dbContext.Users
                       .AsNoTracking()
                       .SingleAsync(candidate => candidate.Id == userId, cancellationToken);
        var roles = await dbContext.UserRoles
            .AsNoTracking()
            .Where(userRole => userRole.UserId == userId)
            .Select(userRole => userRole.Role.Name)
            .ToListAsync(cancellationToken);
        var http = httpContextAccessor.HttpContext;
        var now = timeProvider.GetUtcNow();
        var sessionId = await sessionService.CreateAsync(
            new NewSessionRequest
            {
                UserId = userId,
                AuthenticationMethods = methods,
                IpAddress = http?.Connection.RemoteIpAddress?.ToString(),
                UserAgent = http?.Request.Headers.UserAgent.ToString(),
                DeviceLabel = DeviceLabel(http?.Request.Headers.UserAgent.ToString()),
            },
            cancellationToken);
        var refresh = await refreshTokenService.IssueAsync(sessionId, cancellationToken);
        var access = await accessTokenIssuer.IssueAsync(
            new AccessTokenRequest
            {
                UserId = userId,
                SessionId = sessionId,
                EmailVerified = user.EmailVerified,
                Roles = roles,
                AuthenticationMethods = methods,
                AuthenticatedAt = now,
            },
            cancellationToken);

        var response = new LoginResponse
        {
            TokenType = "Bearer",
            ExpiresAt = access.ExpiresAt,
            User = new UserSummary
            {
                Id = user.Id,
                Email = user.Email,
                EmailVerified = user.EmailVerified,
                DisplayName = user.DisplayName,
                Roles = roles,
            },
        };

        return transport.DeliverLogin(response, sessionId, access.Value, refresh.Value);
    }

    private static string? DeviceLabel(string? userAgent) =>
        string.IsNullOrWhiteSpace(userAgent)
            ? null
            : userAgent.Length <= 100 ? userAgent : userAgent[..100];
}
