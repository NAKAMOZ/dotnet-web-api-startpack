using System.Linq.Expressions;
using Api.Data;
using Api.DTOs.Admin;
using Api.DTOs.Common;
using Api.Exceptions;
using Api.Helpers;
using Api.Models;
using Api.Models.Enums;
using Api.Services.Audit;
using Api.Services.Tokens;
using Microsoft.EntityFrameworkCore;

namespace Api.Services.Users;

public sealed class AdminUserService(
    AppDbContext dbContext,
    ISessionService sessionService,
    IAuditLogger auditLogger,
    TimeProvider timeProvider) : IAdminUserService
{
    private static readonly IReadOnlyDictionary<string, Expression<Func<User, object?>>> Sorts =
        new Dictionary<string, Expression<Func<User, object?>>>(StringComparer.Ordinal)
        {
            ["email"] = user => user.Email,
            ["createdAt"] = user => user.CreatedAt,
            ["emailVerified"] = user => user.EmailVerified,
        };

    public async Task<PagedResponse<AdminUserResponse>> ListAsync(
        AdminUserListQuery query,
        CancellationToken cancellationToken)
    {
        var users = dbContext.Users
            .AsNoTracking()
            .Include(user => user.UserRoles)
            .ThenInclude(userRole => userRole.Role)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var pattern = $"%{query.Search.Trim()}%";
            users = users.Where(user =>
                EF.Functions.ILike(user.Email, pattern)
                || (user.DisplayName != null && EF.Functions.ILike(user.DisplayName, pattern)));
        }

        if (!string.IsNullOrWhiteSpace(query.Role))
        {
            users = users.Where(user => user.UserRoles.Any(userRole => userRole.Role.Name == query.Role));
        }

        if (query.EmailVerified is { } verified)
        {
            users = users.Where(user => user.EmailVerified == verified);
        }

        if (query.Locked is { } locked)
        {
            var now = timeProvider.GetUtcNow();
            users = locked
                ? users.Where(user => user.LockoutEndsAt > now)
                : users.Where(user => user.LockoutEndsAt == null || user.LockoutEndsAt <= now);
        }

        return await users
            .ApplySort(query.Sort, Sorts, user => user.CreatedAt)
            .ToPagedResponseAsync(query, ToListResponse, cancellationToken);
    }

    public async Task<AdminUserDetailResponse> GetAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var user = await dbContext.Users
                       .AsNoTracking()
                       .Include(candidate => candidate.UserRoles)
                       .ThenInclude(userRole => userRole.Role)
                       .Include(candidate => candidate.Accounts)
                       .Include(candidate => candidate.TotpCredential)
                       .SingleOrDefaultAsync(candidate => candidate.Id == userId, cancellationToken)
                   ?? throw new ResourceNotFoundException("user");
        var sessions = await dbContext.Sessions
            .AsNoTracking()
            .Where(session => session.UserId == userId && session.RevokedAt == null)
            .OrderByDescending(session => session.LastActiveAt)
            .Select(session => new SessionSummary
            {
                Id = session.Id,
                DeviceLabel = session.DeviceLabel,
                IpAddress = session.IpAddress,
                CreatedAt = session.CreatedAt,
                LastActiveAt = session.LastActiveAt,
                AbsoluteExpiresAt = session.AbsoluteExpiresAt,
                RevokedAt = session.RevokedAt,
            })
            .ToListAsync(cancellationToken);

        return new AdminUserDetailResponse
        {
            Id = user.Id,
            Email = user.Email,
            EmailVerified = user.EmailVerified,
            DisplayName = user.DisplayName,
            Roles = [.. user.UserRoles.Select(userRole => userRole.Role.Name)],
            LockoutEndsAt = user.LockoutEndsAt,
            FailedLoginCount = user.FailedLoginCount,
            MfaEnabled = user.TotpCredential?.ConfirmedAt is not null,
            HasPassword = user.PasswordHash is not null,
            LinkedProviders = [.. user.Accounts.Select(account => account.Provider)],
            ActiveSessions = sessions,
            CreatedAt = user.CreatedAt,
        };
    }

    public async Task<AdminUserDetailResponse> UpdateAsync(
        Guid userId,
        AdminUpdateUserRequest request,
        CancellationToken cancellationToken)
    {
        var user = await dbContext.Users.SingleOrDefaultAsync(
                       candidate => candidate.Id == userId,
                       cancellationToken)
                   ?? throw new ResourceNotFoundException("user");

        if (request.DisplayName is not null)
        {
            user.DisplayName = request.DisplayName.Trim();
        }

        if (request.EmailVerified is { } verified)
        {
            user.EmailVerified = verified;
        }

        if (request.Unlock is true)
        {
            user.FailedLoginCount = 0;
            user.LockoutEndsAt = null;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return await GetAsync(userId, cancellationToken);
    }

    public async Task DeleteAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await dbContext.Users.SingleOrDefaultAsync(
                       candidate => candidate.Id == userId,
                       cancellationToken)
                   ?? throw new ResourceNotFoundException("user");
        await sessionService.RevokeAllForUserAsync(
            userId,
            null,
            SessionRevocationReason.AccountDeleted,
            cancellationToken);
        dbContext.Users.Remove(user);
        await dbContext.SaveChangesAsync(cancellationToken);
        await auditLogger.LogAsync(
            AuditEventType.AdminUserDeleted,
            null,
            new { DeletedUserId = userId },
            cancellationToken);
    }

    private static AdminUserResponse ToListResponse(User user) =>
        new()
        {
            Id = user.Id,
            Email = user.Email,
            EmailVerified = user.EmailVerified,
            DisplayName = user.DisplayName,
            Roles = [.. user.UserRoles.Select(userRole => userRole.Role.Name)],
            LockoutEndsAt = user.LockoutEndsAt,
            CreatedAt = user.CreatedAt,
        };
}
