using Api.DTOs.Auth;
using Api.Models.Enums;

namespace Api.Services.Auth;

public interface IAuthenticationSessionFactory
{
    Task<LoginResponse> CreateAsync(
        Guid userId,
        IReadOnlyCollection<AuthenticationMethod> methods,
        CancellationToken cancellationToken);
}
