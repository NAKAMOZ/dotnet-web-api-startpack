using Api.DTOs.Auth;

namespace Api.Services.Auth;

public interface IRegistrationService
{
    Task<RegisterResponse> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken);
}
