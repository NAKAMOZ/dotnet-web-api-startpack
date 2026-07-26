using Api.DTOs.Sessions;

namespace Api.Services.Users;

public interface IAdminSessionService
{
    Task<RevokeSessionsResponse> RevokeAllAsync(
        Guid userId,
        CancellationToken cancellationToken);
}
