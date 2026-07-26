using Api.Data;
using Api.Exceptions;
using Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Api.Services.Users;

public sealed class AdminRoleService(AppDbContext dbContext) : IAdminRoleService
{
    public async Task GrantAsync(
        Guid userId,
        Guid roleId,
        CancellationToken cancellationToken)
    {
        await EnsureTargetsAsync(userId, roleId, cancellationToken);
        var exists = await dbContext.UserRoles.AnyAsync(
            userRole => userRole.UserId == userId && userRole.RoleId == roleId,
            cancellationToken);

        if (!exists)
        {
            dbContext.UserRoles.Add(new UserRole { UserId = userId, RoleId = roleId });
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task RevokeAsync(
        Guid userId,
        Guid roleId,
        CancellationToken cancellationToken)
    {
        var assignment = await dbContext.UserRoles.SingleOrDefaultAsync(
                             userRole => userRole.UserId == userId && userRole.RoleId == roleId,
                             cancellationToken)
                         ?? throw new ResourceNotFoundException("role assignment");
        dbContext.UserRoles.Remove(assignment);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task EnsureTargetsAsync(
        Guid userId,
        Guid roleId,
        CancellationToken cancellationToken)
    {
        if (!await dbContext.Users.AnyAsync(user => user.Id == userId, cancellationToken))
        {
            throw new ResourceNotFoundException("user");
        }

        if (!await dbContext.Roles.AnyAsync(role => role.Id == roleId, cancellationToken))
        {
            throw new ResourceNotFoundException("role");
        }
    }
}
