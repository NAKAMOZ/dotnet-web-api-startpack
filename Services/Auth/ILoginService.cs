using Api.DTOs.Auth;

namespace Api.Services.Auth;

public sealed record LoginResult(LoginResponse? Login, MfaChallengeResponse? Challenge)
{
    public static LoginResult Completed(LoginResponse response) => new(response, null);

    public static LoginResult MfaRequired(MfaChallengeResponse response) => new(null, response);
}

public interface ILoginService
{
    Task<LoginResult> LoginAsync(LoginRequest request, CancellationToken cancellationToken);

    Task<LoginResponse> CompleteMfaAsync(
        MfaLoginRequest request,
        CancellationToken cancellationToken);
}
