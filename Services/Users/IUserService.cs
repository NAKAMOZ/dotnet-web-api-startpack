using Api.DTOs.Users;

namespace Api.Services.Users;

public interface IUserService
{
    Task<UserProfileResponse> GetProfileAsync(Guid userId, CancellationToken cancellationToken);

    Task<UserProfileResponse> UpdateProfileAsync(
        Guid userId,
        UpdateProfileRequest request,
        CancellationToken cancellationToken);

    Task DeleteAccountAsync(Guid userId, CancellationToken cancellationToken);

    Task ChangePasswordAsync(
        Guid userId,
        Guid currentSessionId,
        ChangePasswordRequest request,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<LinkedAccountResponse>> ListAccountsAsync(
        Guid userId,
        CancellationToken cancellationToken);

    Task UnlinkAccountAsync(
        Guid userId,
        Guid accountId,
        CancellationToken cancellationToken);
}
