using Api.Data;
using Api.DTOs.EmailVerification;
using Api.Exceptions;
using Api.Models;
using Api.Models.Enums;
using Api.Services.Audit;
using Api.Services.Crypto;
using Api.Services.Email;
using Microsoft.EntityFrameworkCore;

namespace Api.Services.Auth;

public sealed class EmailVerificationService(
    AppDbContext dbContext,
    ITokenGenerator tokenGenerator,
    IEmailSender emailSender,
    IEmailTemplateRenderer templates,
    IAuditLogger auditLogger,
    TimeProvider timeProvider) : IEmailVerificationService
{
    private static readonly TimeSpan TokenLifetime = TimeSpan.FromHours(24);

    public async Task SendAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await dbContext.Users.SingleOrDefaultAsync(
                       candidate => candidate.Id == userId,
                       cancellationToken)
                   ?? throw new ResourceNotFoundException("user");

        if (user.EmailVerified)
        {
            throw new ConflictException("email_already_verified", "The email address is already verified.");
        }

        var plaintext = tokenGenerator.NewOpaqueToken();
        dbContext.VerificationTokens.Add(new VerificationToken
        {
            UserId = user.Id,
            Type = VerificationTokenType.EmailVerification,
            TokenHash = tokenGenerator.Hash(plaintext),
            ExpiresAt = timeProvider.GetUtcNow() + TokenLifetime,
        });
        await dbContext.SaveChangesAsync(cancellationToken);

        await emailSender.QueueAsync(
            new EmailMessage(
                user.Email,
                "Verify your email address",
                templates.Render("EmailVerification", new Dictionary<string, string>
                {
                    ["Token"] = plaintext,
                })),
            cancellationToken);
    }

    public async Task<EmailVerifiedResponse> ConfirmAsync(
        string token,
        CancellationToken cancellationToken)
    {
        var tokenHash = tokenGenerator.Hash(token);
        var now = timeProvider.GetUtcNow();

        var consumed = await dbContext.VerificationTokens
            .Where(candidate => candidate.TokenHash == tokenHash
                                && candidate.Type == VerificationTokenType.EmailVerification
                                && candidate.ConsumedAt == null
                                && candidate.ExpiresAt > now)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(candidate => candidate.ConsumedAt, now),
                cancellationToken);

        if (consumed == 0)
        {
            throw new InvalidTokenException();
        }

        var verification = await dbContext.VerificationTokens
            .AsNoTracking()
            .SingleAsync(candidate => candidate.TokenHash == tokenHash, cancellationToken);
        var user = await dbContext.Users.SingleAsync(
            candidate => candidate.Id == verification.UserId,
            cancellationToken);
        user.EmailVerified = true;
        await dbContext.SaveChangesAsync(cancellationToken);
        await auditLogger.LogAsync(AuditEventType.EmailVerified, user.Id, cancellationToken: cancellationToken);

        return new EmailVerifiedResponse { Email = user.Email, VerifiedAt = now };
    }
}
