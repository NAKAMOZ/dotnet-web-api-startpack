using Api.Data;
using Api.DTOs.Auth;
using Api.Exceptions;
using Api.Logging;
using Api.Models;
using Api.Models.Enums;
using Api.Services.Audit;
using Api.Services.Crypto;
using Api.Services.Mfa;
using Api.Services.Security;
using Api.Services.Tokens;
using Microsoft.EntityFrameworkCore;

namespace Api.Services.Auth;

public sealed class LoginService(
    AppDbContext dbContext,
    IPasswordHasher passwordHasher,
    DummyPasswordHash dummyPasswordHash,
    LockoutPolicy lockoutPolicy,
    IMfaTicketService mfaTicketService,
    ITotpService totpService,
    IAuthenticationSessionFactory sessionFactory,
    IAuditLogger auditLogger,
    AuthMetrics metrics) : ILoginService
{
    public async Task<LoginResult> LoginAsync(
        LoginRequest request,
        CancellationToken cancellationToken)
    {
        var user = await dbContext.Users
            .Include(candidate => candidate.TotpCredential)
            .SingleOrDefaultAsync(candidate => candidate.Email == request.Email.Trim(), cancellationToken);

        var locked = user is not null && lockoutPolicy.IsLockedOut(user);
        var hash = user?.PasswordHash ?? dummyPasswordHash.Value;
        var verified = passwordHasher.Verify(request.Password, hash);

        if (user is null || user.PasswordHash is null || !verified || locked || !user.EmailVerified)
        {
            await RejectAsync(user, locked, cancellationToken);
        }

        lockoutPolicy.RegisterSuccess(user!);

        if (passwordHasher.NeedsRehash(user!.PasswordHash!))
        {
            user.PasswordHash = passwordHasher.Hash(request.Password);
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        if (user.TotpCredential?.ConfirmedAt is not null)
        {
            var ticket = await mfaTicketService.IssueAsync(user.Id, cancellationToken);
            await auditLogger.LogAsync(
                AuditEventType.MfaChallengeIssued,
                user.Id,
                cancellationToken: cancellationToken);
            metrics.RecordMfaChallenge("issued");

            return LoginResult.MfaRequired(new MfaChallengeResponse
            {
                MfaTicket = ticket.Value,
                ExpiresAt = ticket.ExpiresAt,
                AcceptedMethods = ["totp", "recovery"],
            });
        }

        var response = await sessionFactory.CreateAsync(
            user.Id,
            [AuthenticationMethod.Password],
            cancellationToken);
        await auditLogger.LogAsync(AuditEventType.LoginSucceeded, user.Id, cancellationToken: cancellationToken);
        metrics.RecordLogin("success");
        return LoginResult.Completed(response);
    }

    public async Task<LoginResponse> CompleteMfaAsync(
        MfaLoginRequest request,
        CancellationToken cancellationToken)
    {
        var userId = await mfaTicketService.ConsumeAsync(request.MfaTicket, cancellationToken);

        if (userId is null)
        {
            await MfaFailureAsync(null, cancellationToken);
        }

        var method = await totpService.VerifyAsync(userId!.Value, request.Code, cancellationToken);

        if (method is null)
        {
            await MfaFailureAsync(userId, cancellationToken);
        }

        var response = await sessionFactory.CreateAsync(
            userId.Value,
            [AuthenticationMethod.Password, method!.Value],
            cancellationToken);
        await auditLogger.LogAsync(
            AuditEventType.LoginSucceeded,
            userId,
            new { Mfa = method.ToString() },
            cancellationToken);
        metrics.RecordMfaChallenge("success");
        metrics.RecordLogin("success");
        return response;
    }

    private async Task RejectAsync(
        User? user,
        bool alreadyLocked,
        CancellationToken cancellationToken)
    {
        if (user is not null && !alreadyLocked)
        {
            var newlyLocked = lockoutPolicy.RegisterFailure(user);
            await dbContext.SaveChangesAsync(cancellationToken);

            if (newlyLocked)
            {
                await auditLogger.LogAsync(
                    AuditEventType.AccountLocked,
                    user.Id,
                    cancellationToken: cancellationToken);
            }
        }

        await auditLogger.LogAsync(
            AuditEventType.LoginFailed,
            user?.Id,
            cancellationToken: cancellationToken);
        metrics.RecordLogin("failure");
        throw new InvalidCredentialsException();
    }

    private async Task MfaFailureAsync(Guid? userId, CancellationToken cancellationToken)
    {
        await auditLogger.LogAsync(AuditEventType.MfaFailed, userId, cancellationToken: cancellationToken);
        metrics.RecordMfaChallenge("failure");
        throw new InvalidTokenException();
    }
}
