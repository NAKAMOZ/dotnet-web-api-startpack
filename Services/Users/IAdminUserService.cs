using Api.DTOs.Admin;
using Api.DTOs.Common;

namespace Api.Services.Users;

public interface IAdminUserService
{
    Task<PagedResponse<AdminUserResponse>> ListAsync(
        AdminUserListQuery query,
        CancellationToken cancellationToken);

    Task<AdminUserDetailResponse> GetAsync(
        Guid userId,
        CancellationToken cancellationToken);

    Task<AdminUserDetailResponse> UpdateAsync(
        Guid userId,
        AdminUpdateUserRequest request,
        CancellationToken cancellationToken);

    Task DeleteAsync(Guid userId, CancellationToken cancellationToken);
}
