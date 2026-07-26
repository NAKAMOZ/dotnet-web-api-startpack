using Api.DTOs.Auth;

namespace Api.Services.Auth;

public interface IAuthTokenTransport
{
    LoginResponse DeliverLogin(LoginResponse response, Guid sessionId, string accessToken, string refreshToken);

    TokenPairResponse DeliverRefresh(
        TokenPairResponse response,
        Guid sessionId,
        string accessToken,
        string refreshToken);

    string? ReadRefreshToken(string? bodyToken);

    CsrfTokenResponse IssueCsrf(Guid sessionId);

    void ClearCookies();
}
