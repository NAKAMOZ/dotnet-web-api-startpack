using Api.Data;
using Api.DTOs.Users;
using Api.Exceptions;
using Api.Models;
using Api.Models.Enums;
using Api.Services.Audit;
using Api.Services.Crypto;
using Api.Services.Tokens;
using Microsoft.EntityFrameworkCore;

namespace Api.Services.Users;

public sealed class UserService(
    AppDbContext dbContext,
    IPasswordHasher passwordHasher,
    ISessionService sessionService,
    IAuditLogger auditLogger) : IUserService
{
    public async Task<UserProfileResponse> GetProfileAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var user = await ProfileQuery()
                       .SingleOrDefaultAsync(candidate => candidate.Id == userId, cancellationToken)
                   ?? throw new ResourceNotFoundException("user");
        return ToProfile(user);
    }

    public async Task<UserProfileResponse> UpdateProfileAsync(
        Guid userId,
        UpdateProfileRequest request,
        CancellationToken cancellationToken)
    {
        var user = await dbContext.Users.SingleOrDefaultAsync(
                       candidate => candidate.Id == userId,
                       cancellationToken)
                   ?? throw new ResourceNotFoundException("user");
        user.DisplayName = request.DisplayName?.Trim();
        await dbContext.SaveChangesAsync(cancellationToken);
        return await GetProfileAsync(userId, cancellationToken);
    }

    public async Task DeleteAccountAsync(Guid userId, CancellationToken cancellationToken)
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
    }

    public async Task ChangePasswordAsync(
        Guid userId,
        Guid currentSessionId,
        ChangePasswordRequest request,
        CancellationToken cancellationToken)
    {
        var user = await dbContext.Users.SingleOrDefaultAsync(
                       candidate => candidate.Id == userId,
                       cancellationToken)
                   ?? throw new ResourceNotFoundException("user");

        if (user.PasswordHash is null
            || !passwordHasher.Verify(request.CurrentPassword, user.PasswordHash))
        {
            throw new InvalidCredentialsException();
        }

        user.PasswordHash = passwordHasher.Hash(request.NewPassword);
        user.SecurityStamp = Guid.CreateVersion7().ToString("N");

        // The current session remains refreshable; all others keep their old stamp and are
        // explicitly revoked.
        await dbContext.Sessions
            .Where(session => session.Id == currentSessionId && session.UserId == userId)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(session => session.SecurityStamp, user.SecurityStamp),
                cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        var revoked = await sessionService.RevokeAllForUserAsync(
            userId,
            currentSessionId,
            SessionRevocationReason.PasswordChanged,
            cancellationToken);
        await auditLogger.LogAsync(
            AuditEventType.PasswordChanged,
            userId,
            new { RevokedSessions = revoked },
            cancellationToken);
    }

    public async Task<IReadOnlyList<LinkedAccountResponse>> ListAccountsAsync(
        Guid userId,
        CancellationToken cancellationToken) =>
        await dbContext.Accounts
            .AsNoTracking()
            .Where(account => account.UserId == userId)
            .OrderBy(account => account.Provider)
            .Select(account => new LinkedAccountResponse
            {
                Id = account.Id,
                Provider = account.Provider,
                LinkedAt = account.CreatedAt,
            })
            .ToListAsync(cancellationToken);

    public async Task UnlinkAccountAsync(
        Guid userId,
        Guid accountId,
        CancellationToken cancellationToken)
    {
        var account = await dbContext.Accounts.SingleOrDefaultAsync(
                          candidate => candidate.Id == accountId && candidate.UserId == userId,
                          cancellationToken)
                      ?? throw new ResourceNotFoundException("linked account");
        var hasAlternative = await dbContext.Users
            .Where(user => user.Id == userId)
            .AnyAsync(
                user => user.PasswordHash != null
                        || user.PasskeyCredentials.Any()
                        || user.Accounts.Any(candidate => candidate.Id != accountId),
                cancellationToken);

        if (!hasAlternative)
        {
            throw new ConflictException(
                "last_authentication_method",
                "The last authentication method cannot be removed.");
        }

        dbContext.Accounts.Remove(account);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private IQueryable<User> ProfileQuery() =>
        dbContext.Users
            .AsNoTracking()
            .Include(user => user.UserRoles)
            .ThenInclude(userRole => userRole.Role)
            .Include(user => user.TotpCredential)
            .Include(user => user.PasskeyCredentials);

    private static UserProfileResponse ToProfile(User user) =>
        new()
        {
            Id = user.Id,
            Email = user.Email,
            EmailVerified = user.EmailVerified,
            DisplayName = user.DisplayName,
            Roles = [.. user.UserRoles.Select(userRole => userRole.Role.Name)],
            MfaEnabled = user.TotpCredential?.ConfirmedAt is not null,
            PasskeyCount = user.PasskeyCredentials.Count,
            HasPassword = user.PasswordHash is not null,
            CreatedAt = user.CreatedAt,
        };
}
