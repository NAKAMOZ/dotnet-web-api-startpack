using Api.DTOs.ApiKeys;

namespace Api.Services.ApiKeys;

public interface IApiKeyService
{
    Task<CreateApiKeyResponse> CreateAsync(
        Guid userId,
        CreateApiKeyRequest request,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<ApiKeySummaryResponse>> ListAsync(
        Guid userId,
        CancellationToken cancellationToken);

    Task RevokeAsync(Guid userId, Guid keyId, CancellationToken cancellationToken);
}
