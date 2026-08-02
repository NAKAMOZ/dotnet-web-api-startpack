using System.Text;
using Api.Data;
using Api.DTOs.ApiKeys;
using Api.Exceptions;
using Api.Handlers.Authorization;
using Api.Models;
using Api.Services.Crypto;
using Api.Services.Email;
using Microsoft.EntityFrameworkCore;

namespace Api.Services.ApiKeys;

public sealed class ApiKeyService(
    AppDbContext dbContext,
    IPasswordHasher passwordHasher,
    ITokenGenerator tokenGenerator,
    ISecurityNotificationService securityNotifications,
    TimeProvider timeProvider) : IApiKeyService
{
    public async Task<CreateApiKeyResponse> CreateAsync(
        Guid userId,
        CreateApiKeyRequest request,
        CancellationToken cancellationToken)
    {
        var roles = await dbContext.UserRoles
            .Where(userRole => userRole.UserId == userId)
            .Select(userRole => userRole.Role.Name)
            .ToListAsync(cancellationToken);
        var granted = roles
            .SelectMany(RolePermissionMap.PermissionsFor)
            .ToHashSet(StringComparer.Ordinal);

        if (request.Scopes.Any(scope => !granted.Contains(scope)))
        {
            throw new ForbiddenOperationException();
        }

        var prefix = CreatePrefix();
        var secret = tokenGenerator.NewOpaqueToken();
        var now = timeProvider.GetUtcNow();
        var key = new ApiKey
        {
            UserId = userId,
            Name = request.Name.Trim(),
            KeyPrefix = prefix,
            KeyHash = passwordHasher.HashSecret(secret),
            Scopes = [.. request.Scopes.Distinct(StringComparer.Ordinal)],
            ExpiresAt = request.ExpiresAt,
        };
        dbContext.ApiKeys.Add(key);
        await dbContext.SaveChangesAsync(cancellationToken);
        await securityNotifications.NotifyAsync(
            userId,
            SecurityNotificationType.ApiKeyCreated,
            cancellationToken);

        return new CreateApiKeyResponse
        {
            Id = key.Id,
            Name = key.Name,
            Key = $"ak_{prefix}_{secret}",
            KeyPrefix = prefix,
            Scopes = [.. key.Scopes],
            ExpiresAt = key.ExpiresAt,
            CreatedAt = key.CreatedAt == default ? now : key.CreatedAt,
        };
    }

    public async Task<IReadOnlyList<ApiKeySummaryResponse>> ListAsync(
        Guid userId,
        CancellationToken cancellationToken) =>
        await dbContext.ApiKeys
            .AsNoTracking()
            .Where(key => key.UserId == userId)
            .OrderByDescending(key => key.CreatedAt)
            .Select(key => new ApiKeySummaryResponse
            {
                Id = key.Id,
                Name = key.Name,
                KeyPrefix = key.KeyPrefix,
                Scopes = key.Scopes.ToArray(),
                ExpiresAt = key.ExpiresAt,
                LastUsedAt = key.LastUsedAt,
                RevokedAt = key.RevokedAt,
                CreatedAt = key.CreatedAt,
            })
            .ToListAsync(cancellationToken);

    public async Task RevokeAsync(
        Guid userId,
        Guid keyId,
        CancellationToken cancellationToken)
    {
        var key = await dbContext.ApiKeys.SingleOrDefaultAsync(
                      candidate => candidate.Id == keyId
                                   && candidate.UserId == userId
                                   && candidate.RevokedAt == null,
                      cancellationToken)
                  ?? throw new ResourceNotFoundException("API key");
        key.RevokedAt = timeProvider.GetUtcNow();
        await dbContext.SaveChangesAsync(cancellationToken);
        await securityNotifications.NotifyAsync(
            userId,
            SecurityNotificationType.ApiKeyRevoked,
            cancellationToken);
    }

    private string CreatePrefix()
    {
        // '_' is the credential delimiter and is also legal base64url. Letting it into the
        // prefix makes a random subset of otherwise valid keys unparsable by the handler.
        var prefix = new StringBuilder(capacity: 12);

        while (prefix.Length < 12)
        {
            foreach (var character in tokenGenerator.NewOpaqueToken())
            {
                if (char.IsAsciiLetterOrDigit(character))
                {
                    prefix.Append(character);

                    if (prefix.Length == 12)
                    {
                        break;
                    }
                }
            }
        }

        return prefix.ToString();
    }
}
