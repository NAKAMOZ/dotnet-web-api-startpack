using Api.DTOs.Auth;
using Api.DTOs.Passkeys;

namespace Api.Services.Passkeys;

public interface IPasskeyService
{
    Task<PasskeyRegistrationOptionsResponse> RegistrationOptionsAsync(
        Guid userId,
        PasskeyRegistrationOptionsRequest request,
        CancellationToken cancellationToken);

    Task<PasskeyResponse> CompleteRegistrationAsync(
        Guid userId,
        PasskeyRegistrationRequest request,
        CancellationToken cancellationToken);

    Task<PasskeyAuthenticationOptionsResponse> AuthenticationOptionsAsync(
        CancellationToken cancellationToken);

    Task<LoginResponse> CompleteAuthenticationAsync(
        PasskeyAuthenticationRequest request,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<PasskeyResponse>> ListAsync(
        Guid userId,
        CancellationToken cancellationToken);

    Task RemoveAsync(
        Guid userId,
        string credentialId,
        CancellationToken cancellationToken);
}
