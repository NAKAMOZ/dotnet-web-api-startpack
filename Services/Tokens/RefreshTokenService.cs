using Api.Configuration;
using Api.Data;
using Api.Models;
using Api.Models.Enums;
using Api.Services.Crypto;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Api.Services.Tokens;

/// <inheritdoc cref="IRefreshTokenService"/>
public sealed class RefreshTokenService(
    AppDbContext dbContext,
    ITokenGenerator tokenGenerator,
    IAccessTokenIssuer accessTokenIssuer,
    IOptions<AuthSessionOptions> sessionOptions,
    TimeProvider timeProvider,
    ILogger<RefreshTokenService> logger) : IRefreshTokenService
{
    private readonly AuthSessionOptions _options = sessionOptions.Value;

    public async Task<IssuedRefreshToken> IssueAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        var session = await dbContext.Sessions
            .AsNoTracking()
            .SingleAsync(candidate => candidate.Id == sessionId, cancellationToken);

        var (token, entity) = CreateToken(sessionId, session.AbsoluteExpiresAt);

        dbContext.RefreshTokens.Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken);

        return token;
    }

    public async Task<RefreshResult> RotateAsync(string presentedToken, CancellationToken cancellationToken)
    {
        var presentedHash = tokenGenerator.Hash(presentedToken);

        // EnableRetryOnFailure is on, so a self-opened transaction must run through the
        // execution strategy or EF throws. The whole block is re-runnable, which is why it
        // owns the transaction rather than sitting inside one (DataAccess.md §5).
        var strategy = dbContext.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

            var stored = await dbContext.RefreshTokens
                .Include(token => token.Session)
                .SingleOrDefaultAsync(token => token.TokenHash == presentedHash, cancellationToken);

            if (stored is null)
            {
                return RefreshResult.Failure(RefreshOutcome.NotFound);
            }

            var session = stored.Session;
            var now = timeProvider.GetUtcNow();

            // Reuse comes first, before every other check. A replayed token that is also
            // expired must still trigger revocation — treating it as a plain expiry would
            // let an attacker's replay pass unnoticed by being slightly late.
            if (stored.UsedAt is not null)
            {
                logger.LogWarning(
                    "Refresh token reuse detected on session {SessionId}. Revoking the session.",
                    session.Id);

                session.RevokedAt = now;
                session.RevocationReason = SessionRevocationReason.TokenReuseDetected;

                await dbContext.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);

                return RefreshResult.Failure(RefreshOutcome.ReuseDetected, session.Id);
            }

            if (session.RevokedAt is not null)
            {
                return RefreshResult.Failure(RefreshOutcome.SessionRevoked, session.Id);
            }

            if (stored.ExpiresAt <= now)
            {
                return RefreshResult.Failure(RefreshOutcome.TokenExpired, session.Id);
            }

            // The two session bounds are reported separately so a client can tell "you were
            // idle" from "your session aged out" — ADR-0002 requires the distinction to
            // exist in the service contract, not just in the HTTP layer.
            if (session.AbsoluteExpiresAt <= now)
            {
                return RefreshResult.Failure(RefreshOutcome.SessionExpired, session.Id);
            }

            if (session.LastActiveAt + _options.InactivityWindow <= now)
            {
                return RefreshResult.Failure(RefreshOutcome.SessionIdle, session.Id);
            }

            var currentStamp = await dbContext.Users
                .Where(user => user.Id == session.UserId)
                .Select(user => user.SecurityStamp)
                .SingleAsync(cancellationToken);

            // Checked here rather than per request. That keeps access-token validation
            // stateless while still giving a per-user kill switch that takes effect within
            // one access-token lifetime (Authentication.md §6).
            if (!tokenGenerator.FixedTimeEquals(currentStamp, session.SecurityStamp))
            {
                return RefreshResult.Failure(RefreshOutcome.SecurityStampChanged, session.Id);
            }

            var (issuedRefresh, successor) = CreateToken(session.Id, session.AbsoluteExpiresAt);

            stored.UsedAt = now;
            stored.ReplacedByTokenId = successor.Id;
            dbContext.RefreshTokens.Add(successor);

            // Slides the inactivity window only. AbsoluteExpiresAt is untouched — a session
            // that refreshes every minute for a week still dies at the cap.
            session.LastActiveAt = now;

            var roles = await dbContext.UserRoles
                .Where(userRole => userRole.UserId == session.UserId)
                .Select(userRole => userRole.Role.Name)
                .ToListAsync(cancellationToken);

            var user = await dbContext.Users
                .AsNoTracking()
                .SingleAsync(candidate => candidate.Id == session.UserId, cancellationToken);

            var accessToken = await accessTokenIssuer.IssueAsync(
                new AccessTokenRequest
                {
                    UserId = session.UserId,
                    SessionId = session.Id,
                    EmailVerified = user.EmailVerified,
                    Roles = roles,
                    AuthenticationMethods = [.. session.AuthenticationMethods],

                    // Carried forward from the session, NOT set to now. This is the whole
                    // step-up guarantee: rotating tokens must not look like re-authenticating.
                    AuthenticatedAt = session.AuthenticatedAt,
                },
                cancellationToken);

            // Marking used, linking the successor and sliding the session commit together.
            // A partial rotation either burns a token without issuing a replacement, or
            // leaves two live tokens on one chain — which makes reuse detection unreliable.
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return RefreshResult.Success(session.Id, accessToken, issuedRefresh);
        });
    }

    public async Task RevokeForSessionAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        // Expiry is set to now rather than the rows being deleted. A deleted token is
        // indistinguishable from one that never existed, and reuse detection depends on
        // telling those apart (Authentication.md §11).
        var now = timeProvider.GetUtcNow();

        await dbContext.RefreshTokens
            .Where(token => token.SessionId == sessionId && token.UsedAt == null && token.ExpiresAt > now)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(token => token.ExpiresAt, now),
                cancellationToken);
    }

    private (IssuedRefreshToken Issued, RefreshToken Entity) CreateToken(Guid sessionId, DateTimeOffset sessionExpiry)
    {
        var plaintext = tokenGenerator.NewOpaqueToken();

        // Bounded by the session's absolute cap, never beyond it — a token that outlived its
        // session would be a credential with no owner left to revoke it.
        var entity = new RefreshToken
        {
            SessionId = sessionId,
            TokenHash = tokenGenerator.Hash(plaintext),
            ExpiresAt = sessionExpiry,
        };

        return (new IssuedRefreshToken(plaintext, entity.Id, sessionExpiry), entity);
    }
}
