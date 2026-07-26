using Api.Data;
using Api.DTOs.Sessions;
using Api.Exceptions;
using Api.Models.Enums;
using Api.Services.Tokens;
using Microsoft.EntityFrameworkCore;

namespace Api.Services.Users;

public sealed class AdminSessionService(
    AppDbContext dbContext,
    ISessionService sessionService) : IAdminSessionService
{
    public async Task<RevokeSessionsResponse> RevokeAllAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        if (!await dbContext.Users.AnyAsync(user => user.Id == userId, cancellationToken))
        {
            throw new ResourceNotFoundException("user");
        }

        var count = await sessionService.RevokeAllForUserAsync(
            userId,
            null,
            SessionRevocationReason.AdminRevoked,
            cancellationToken);
        return new RevokeSessionsResponse { RevokedCount = count };
    }
}
