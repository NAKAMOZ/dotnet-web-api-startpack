namespace Api.Services.Users;

public interface IAdminRoleService
{
    Task GrantAsync(
        Guid userId,
        Guid roleId,
        CancellationToken cancellationToken);

    Task RevokeAsync(
        Guid userId,
        Guid roleId,
        CancellationToken cancellationToken);
}
