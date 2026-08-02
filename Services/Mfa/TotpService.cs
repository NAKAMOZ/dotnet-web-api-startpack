using System.Security.Cryptography;
using Api.Data;
using Api.DTOs.Mfa;
using Api.Exceptions;
using Api.Models;
using Api.Models.Enums;
using Api.Services.Audit;
using Api.Services.Crypto;
using Api.Services.Email;
using Api.Services.Tokens;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using OtpNet;

namespace Api.Services.Mfa;

public sealed class TotpService(
    AppDbContext dbContext,
    IDataProtectionProvider dataProtectionProvider,
    IPasswordHasher passwordHasher,
    ITokenGenerator tokenGenerator,
    ISessionService sessionService,
    IAuditLogger auditLogger,
    ISecurityNotificationService securityNotifications,
    TimeProvider timeProvider) : ITotpService
{
    private const string ProtectorPurpose = "Api.TotpSecrets.v1";
    private const int RecoveryCodeCount = 10;
    private readonly IDataProtector _protector = dataProtectionProvider.CreateProtector(ProtectorPurpose);

    public async Task<TotpEnrollmentResponse> EnrollAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var user = await dbContext.Users
                       .Include(candidate => candidate.TotpCredential)
                       .SingleOrDefaultAsync(candidate => candidate.Id == userId, cancellationToken)
                   ?? throw new ResourceNotFoundException("user");

        if (user.TotpCredential?.ConfirmedAt is not null)
        {
            throw new ConflictException("mfa_already_enrolled", "TOTP is already enabled.");
        }

        var secretBytes = RandomNumberGenerator.GetBytes(20);
        var secret = Base32Encoding.ToString(secretBytes);
        var encrypted = _protector.Protect(secret);

        if (user.TotpCredential is null)
        {
            dbContext.TotpCredentials.Add(new TotpCredential
            {
                UserId = userId,
                SecretEncrypted = encrypted,
            });
        }
        else
        {
            user.TotpCredential.SecretEncrypted = encrypted;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        var label = Uri.EscapeDataString(user.Email);

        return new TotpEnrollmentResponse
        {
            Secret = secret,
            OtpAuthUri = $"otpauth://totp/dotnet-web-api-startpack:{label}?secret={secret}&issuer=dotnet-web-api-startpack",
            RequiresConfirmation = true,
        };
    }

    public async Task<RecoveryCodesResponse> ConfirmAsync(
        Guid userId,
        string code,
        CancellationToken cancellationToken)
    {
        var credential = await dbContext.TotpCredentials.SingleOrDefaultAsync(
                             candidate => candidate.UserId == userId,
                             cancellationToken)
                         ?? throw new InvalidTokenException();

        var matchedTimeStep = MatchTotp(credential, code);

        if (matchedTimeStep is null)
        {
            throw new InvalidTokenException();
        }

        credential.ConfirmedAt = timeProvider.GetUtcNow();
        credential.LastUsedTimeStep = matchedTimeStep;
        var recoveryCodes = await ReplaceRecoveryCodesAsync(userId, cancellationToken);
        await securityNotifications.NotifyAsync(
            userId,
            SecurityNotificationType.MfaEnabled,
            cancellationToken);
        return recoveryCodes;
    }

    public async Task<AuthenticationMethod?> VerifyAsync(
        Guid userId,
        string code,
        CancellationToken cancellationToken)
    {
        var credential = await dbContext.TotpCredentials
            .AsNoTracking()
            .SingleOrDefaultAsync(
                candidate => candidate.UserId == userId && candidate.ConfirmedAt != null,
                cancellationToken);

        if (credential is null)
        {
            return null;
        }

        if (MatchTotp(credential, code) is { } matchedTimeStep)
        {
            var now = timeProvider.GetUtcNow();
            var claimed = await dbContext.TotpCredentials
                .Where(candidate =>
                    candidate.Id == credential.Id
                    && (candidate.LastUsedTimeStep == null
                        || candidate.LastUsedTimeStep < matchedTimeStep))
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(candidate => candidate.LastUsedTimeStep, matchedTimeStep)
                        .SetProperty(candidate => candidate.UpdatedAt, now),
                    cancellationToken);

            // This conditional update is the replay lock. A second request racing with the
            // first affects zero rows even if both verified the cryptographic code.
            return claimed == 1 ? AuthenticationMethod.Totp : null;
        }

        var recoveryCodes = await dbContext.RecoveryCodes
            .Where(candidate => candidate.UserId == userId && candidate.UsedAt == null)
            .ToListAsync(cancellationToken);
        var recoveryCode = recoveryCodes.FirstOrDefault(candidate =>
            passwordHasher.Verify(code, candidate.CodeHash));

        if (recoveryCode is null)
        {
            return null;
        }

        recoveryCode.UsedAt = timeProvider.GetUtcNow();
        await dbContext.SaveChangesAsync(cancellationToken);
        return AuthenticationMethod.RecoveryCode;
    }

    public async Task DisableAsync(Guid userId, CancellationToken cancellationToken)
    {
        var deleted = await dbContext.TotpCredentials
            .Where(candidate => candidate.UserId == userId && candidate.ConfirmedAt != null)
            .ExecuteDeleteAsync(cancellationToken);

        if (deleted == 0)
        {
            throw new ResourceNotFoundException("totp credential");
        }

        await dbContext.RecoveryCodes
            .Where(candidate => candidate.UserId == userId)
            .ExecuteDeleteAsync(cancellationToken);
        var revoked = await sessionService.RevokeAllForUserAsync(
            userId,
            null,
            SessionRevocationReason.MfaDisabled,
            cancellationToken);

        if (revoked > 0)
        {
            await auditLogger.LogAsync(
                AuditEventType.SessionRevoked,
                userId,
                new { Reason = SessionRevocationReason.MfaDisabled, Count = revoked },
                cancellationToken);
        }

        await securityNotifications.NotifyAsync(
            userId,
            SecurityNotificationType.MfaDisabled,
            cancellationToken);
    }

    public async Task<RecoveryCodesResponse> RegenerateRecoveryCodesAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var enabled = await dbContext.TotpCredentials.AnyAsync(
            candidate => candidate.UserId == userId && candidate.ConfirmedAt != null,
            cancellationToken);

        if (!enabled)
        {
            throw new ConflictException("mfa_not_enrolled", "TOTP is not enabled.");
        }

        var recoveryCodes = await ReplaceRecoveryCodesAsync(userId, cancellationToken);
        await securityNotifications.NotifyAsync(
            userId,
            SecurityNotificationType.RecoveryCodesRegenerated,
            cancellationToken);
        return recoveryCodes;
    }

    private long? MatchTotp(TotpCredential credential, string code)
    {
        var secret = Base32Encoding.ToBytes(_protector.Unprotect(credential.SecretEncrypted));
        var totp = new Totp(secret);
        return totp.VerifyTotp(
            timeProvider.GetUtcNow().UtcDateTime,
            code,
            out var matchedTimeStep,
            new VerificationWindow(previous: 1, future: 1))
            ? matchedTimeStep
            : null;
    }

    private async Task<RecoveryCodesResponse> ReplaceRecoveryCodesAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        await dbContext.RecoveryCodes
            .Where(candidate => candidate.UserId == userId)
            .ExecuteDeleteAsync(cancellationToken);

        var plaintext = Enumerable.Range(0, RecoveryCodeCount)
            .Select(_ => FormatRecoveryCode(tokenGenerator.NewOpaqueToken()))
            .ToArray();

        dbContext.RecoveryCodes.AddRange(plaintext.Select(code => new RecoveryCode
        {
            UserId = userId,
            CodeHash = passwordHasher.HashSecret(code),
        }));
        var generatedAt = timeProvider.GetUtcNow();
        await dbContext.SaveChangesAsync(cancellationToken);

        return new RecoveryCodesResponse { Codes = plaintext, GeneratedAt = generatedAt };
    }

    private static string FormatRecoveryCode(string value) =>
        $"{value[..4]}-{value[4..8]}-{value[8..12]}";
}
