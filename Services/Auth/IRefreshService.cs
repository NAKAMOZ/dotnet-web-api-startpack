using Api.DTOs.Auth;

namespace Api.Services.Auth;

public interface IRefreshService
{
    Task<TokenPairResponse> RefreshAsync(string? bodyToken, CancellationToken cancellationToken);
}
