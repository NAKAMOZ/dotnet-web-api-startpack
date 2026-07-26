using Api.Data;
using Api.Data.Seeding;
using Api.DTOs.Auth;
using Api.Models;
using Api.Models.Enums;
using Api.Services.Audit;
using Api.Services.Crypto;
using Api.Services.Email;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Api.Services.Auth;

public sealed class RegistrationService(
    AppDbContext dbContext,
    IPasswordHasher passwordHasher,
    ITokenGenerator tokenGenerator,
    IEmailSender emailSender,
    IEmailTemplateRenderer templates,
    IAuditLogger auditLogger,
    TimeProvider timeProvider) : IRegistrationService
{
    private static readonly TimeSpan EmailTokenLifetime = TimeSpan.FromHours(24);
    private static readonly TimeSpan ResetTokenLifetime = TimeSpan.FromHours(1);
    private static readonly RegisterResponse AcceptedResponse = new()
    {
        Message = "If the address can be registered, verification instructions will be sent.",
    };

    public async Task<RegisterResponse> RegisterAsync(
        RegisterRequest request,
        CancellationToken cancellationToken)
    {
        var email = request.Email.Trim();

        // Always perform the expensive work before the existence branch. Otherwise this
        // anonymous endpoint becomes a high-signal timing oracle.
        var passwordHash = passwordHasher.Hash(request.Password);
        var existing = await dbContext.Users.SingleOrDefaultAsync(
            user => user.Email == email,
            cancellationToken);

        if (existing is not null)
        {
            return await HandleExistingAsync(existing, cancellationToken);
        }

        var user = new User
        {
            Email = email,
            DisplayName = request.DisplayName?.Trim(),
            PasswordHash = passwordHash,
            EmailVerified = false,
        };
        dbContext.Users.Add(user);
        dbContext.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = RoleSeed.UserRoleId });

        var verificationToken = tokenGenerator.NewOpaqueToken();
        dbContext.VerificationTokens.Add(new VerificationToken
        {
            UserId = user.Id,
            Type = VerificationTokenType.EmailVerification,
            TokenHash = tokenGenerator.Hash(verificationToken),
            ExpiresAt = timeProvider.GetUtcNow() + EmailTokenLifetime,
        });

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsDuplicateEmail(exception))
        {
            // The database unique index is the arbiter for two concurrent registrations.
            // Converge the loser onto the same public and email-notification path instead
            // of leaking the race as a 500.
            dbContext.ChangeTracker.Clear();
            var racedExisting = await dbContext.Users.SingleAsync(
                candidate => candidate.Email == email,
                cancellationToken);
            return await HandleExistingAsync(racedExisting, cancellationToken);
        }

        await auditLogger.LogAsync(AuditEventType.UserRegistered, user.Id, cancellationToken: cancellationToken);
        await emailSender.QueueAsync(
            new EmailMessage(
                user.Email,
                "Verify your email address",
                templates.Render("EmailVerification", new Dictionary<string, string>
                {
                    ["Token"] = verificationToken,
                })),
            cancellationToken);

        return AcceptedResponse;
    }

    private async Task<RegisterResponse> HandleExistingAsync(
        User existing,
        CancellationToken cancellationToken)
    {
        var resetToken = await CreateTokenAsync(
            existing.Id,
            VerificationTokenType.PasswordReset,
            ResetTokenLifetime,
            cancellationToken);
        await emailSender.QueueAsync(
            new EmailMessage(
                existing.Email,
                "Someone tried to register your email address",
                templates.Render("RegistrationAttempt", new Dictionary<string, string>
                {
                    ["Token"] = resetToken,
                })),
            cancellationToken);

        return AcceptedResponse;
    }

    private async Task<string> CreateTokenAsync(
        Guid userId,
        VerificationTokenType type,
        TimeSpan lifetime,
        CancellationToken cancellationToken)
    {
        var plaintext = tokenGenerator.NewOpaqueToken();
        dbContext.VerificationTokens.Add(new VerificationToken
        {
            UserId = userId,
            Type = type,
            TokenHash = tokenGenerator.Hash(plaintext),
            ExpiresAt = timeProvider.GetUtcNow() + lifetime,
        });
        await dbContext.SaveChangesAsync(cancellationToken);
        return plaintext;
    }

    private static bool IsDuplicateEmail(DbUpdateException exception) =>
        exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
            ConstraintName: "IX_Users_Email",
        };

}
