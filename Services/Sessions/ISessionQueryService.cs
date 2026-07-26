using Api.DTOs.Sessions;

namespace Api.Services.Sessions;

public interface ISessionQueryService
{
    Task<IReadOnlyList<SessionResponse>> ListAsync(
        Guid userId,
        Guid currentSessionId,
        CancellationToken cancellationToken);

    Task RevokeAsync(Guid userId, Guid sessionId, CancellationToken cancellationToken);

    Task<RevokeSessionsResponse> RevokeAllOthersAsync(
        Guid userId,
        Guid currentSessionId,
        CancellationToken cancellationToken);
}
