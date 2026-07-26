using Api.DTOs.Auth;
using Api.Exceptions;
using Api.Services.Tokens;

namespace Api.Services.Auth;

public sealed class RefreshService(
    IRefreshTokenService refreshTokenService,
    IAuthTokenTransport transport) : IRefreshService
{
    public async Task<TokenPairResponse> RefreshAsync(
        string? bodyToken,
        CancellationToken cancellationToken)
    {
        var presented = transport.ReadRefreshToken(bodyToken);

        if (string.IsNullOrWhiteSpace(presented))
        {
            throw new InvalidCredentialsException();
        }

        var result = await refreshTokenService.RotateAsync(presented, cancellationToken);

        if (result.Outcome == RefreshOutcome.ReuseDetected && result.SessionId is { } reusedSessionId)
        {
            throw new TokenReuseDetectedException(reusedSessionId);
        }

        if (result.Outcome != RefreshOutcome.Rotated
            || result.SessionId is not { } sessionId
            || result.AccessToken is not { } access
            || result.RefreshToken is not { } refresh)
        {
            throw new InvalidCredentialsException();
        }

        return transport.DeliverRefresh(
            new TokenPairResponse { TokenType = "Bearer", ExpiresAt = access.ExpiresAt },
            sessionId,
            access.Value,
            refresh.Value);
    }
}
