using Api.Data;
using Api.DTOs.PasswordReset;
using Api.Exceptions;
using Api.Models;
using Api.Models.Enums;
using Api.Services.Audit;
using Api.Services.Crypto;
using Api.Services.Email;
using Api.Services.Tokens;
using Microsoft.EntityFrameworkCore;

namespace Api.Services.Auth;

public sealed class PasswordResetService(
    AppDbContext dbContext,
    ITokenGenerator tokenGenerator,
    IPasswordHasher passwordHasher,
    ISessionService sessionService,
    IEmailSender emailSender,
    IEmailTemplateRenderer templates,
    IAuditLogger auditLogger,
    TimeProvider timeProvider) : IPasswordResetService
{
    private static readonly TimeSpan TokenLifetime = TimeSpan.FromHours(1);

    public async Task RequestAsync(
        PasswordResetRequest request,
        CancellationToken cancellationToken)
    {
        var user = await dbContext.Users.SingleOrDefaultAsync(
            candidate => candidate.Email == request.Email.Trim(),
            cancellationToken);

        if (user is null)
        {
            return;
        }

        var plaintext = tokenGenerator.NewOpaqueToken();
        dbContext.VerificationTokens.Add(new VerificationToken
        {
            UserId = user.Id,
            Type = VerificationTokenType.PasswordReset,
            TokenHash = tokenGenerator.Hash(plaintext),
            ExpiresAt = timeProvider.GetUtcNow() + TokenLifetime,
        });
        await dbContext.SaveChangesAsync(cancellationToken);
        await auditLogger.LogAsync(
            AuditEventType.PasswordResetRequested,
            user.Id,
            cancellationToken: cancellationToken);
        await emailSender.QueueAsync(
            new EmailMessage(
                user.Email,
                "Reset your password",
                templates.Render("PasswordReset", new Dictionary<string, string>
                {
                    ["Token"] = plaintext,
                })),
            cancellationToken);
    }

    public async Task ConfirmAsync(
        PasswordResetConfirmRequest request,
        CancellationToken cancellationToken)
    {
        var hash = tokenGenerator.Hash(request.Token);
        var now = timeProvider.GetUtcNow();
        var consumed = await dbContext.VerificationTokens
            .Where(candidate => candidate.TokenHash == hash
                                && candidate.Type == VerificationTokenType.PasswordReset
                                && candidate.ConsumedAt == null
                                && candidate.ExpiresAt > now)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(candidate => candidate.ConsumedAt, now),
                cancellationToken);

        if (consumed == 0)
        {
            throw new InvalidTokenException();
        }

        var userId = await dbContext.VerificationTokens
            .AsNoTracking()
            .Where(candidate => candidate.TokenHash == hash)
            .Select(candidate => candidate.UserId)
            .SingleAsync(cancellationToken)
            ?? throw new InvalidTokenException();
        var user = await dbContext.Users.SingleAsync(
            candidate => candidate.Id == userId,
            cancellationToken);
        user.PasswordHash = passwordHasher.Hash(request.NewPassword);
        user.SecurityStamp = Guid.CreateVersion7().ToString("N");
        await dbContext.SaveChangesAsync(cancellationToken);

        var revoked = await sessionService.RevokeAllForUserAsync(
            userId,
            null,
            SessionRevocationReason.PasswordReset,
            cancellationToken);
        await auditLogger.LogAsync(
            AuditEventType.PasswordResetCompleted,
            userId,
            cancellationToken: cancellationToken);

        if (revoked > 0)
        {
            await auditLogger.LogAsync(
                AuditEventType.SessionRevoked,
                userId,
                new { Reason = SessionRevocationReason.PasswordReset, Count = revoked },
                cancellationToken);
        }
    }
}
