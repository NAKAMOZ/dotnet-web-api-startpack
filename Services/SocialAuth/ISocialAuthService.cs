using Api.DTOs.Auth;
using Api.DTOs.SocialAuth;

namespace Api.Services.SocialAuth;

public interface ISocialAuthService
{
    Task<SocialAuthorizeResponse> AuthorizeAsync(
        string provider,
        CancellationToken cancellationToken);

    Task<LoginResponse> CallbackAsync(
        string provider,
        SocialCallbackQuery query,
        CancellationToken cancellationToken);
}
