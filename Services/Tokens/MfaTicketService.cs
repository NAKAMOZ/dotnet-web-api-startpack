using Api.Configuration;
using Api.Data;
using Api.Models;
using Api.Models.Enums;
using Api.Services.Crypto;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Api.Services.Tokens;

/// <inheritdoc cref="IMfaTicketService"/>
public sealed class MfaTicketService(
    AppDbContext dbContext,
    ITokenGenerator tokenGenerator,
    IOptions<AuthSessionOptions> sessionOptions,
    TimeProvider timeProvider) : IMfaTicketService
{
    private readonly AuthSessionOptions _options = sessionOptions.Value;

    public async Task<IssuedMfaTicket> IssueAsync(Guid userId, CancellationToken cancellationToken)
    {
        var plaintext = tokenGenerator.NewOpaqueToken();
        var expiresAt = timeProvider.GetUtcNow() + _options.MfaTicketLifetime;

        dbContext.VerificationTokens.Add(new VerificationToken
        {
            UserId = userId,
            Type = VerificationTokenType.MfaChallenge,
            TokenHash = tokenGenerator.Hash(plaintext),
            ExpiresAt = expiresAt,
        });

        await dbContext.SaveChangesAsync(cancellationToken);

        return new IssuedMfaTicket(plaintext, expiresAt);
    }

    public async Task<Guid?> ConsumeAsync(string presentedTicket, CancellationToken cancellationToken)
    {
        var hash = tokenGenerator.Hash(presentedTicket);
        var now = timeProvider.GetUtcNow();

        // Validation and consumption in ONE statement. A read-then-write leaves a window in
        // which two concurrent requests both see an unconsumed ticket and both complete the
        // same login — the ticket's single-use guarantee is only as strong as this atomicity.
        //
        // The type is part of the WHERE clause, not checked afterwards: a password-reset
        // token presented here must fail to resolve rather than resolve and then be rejected.
        var consumed = await dbContext.VerificationTokens
            .Where(token => token.TokenHash == hash
                            && token.Type == VerificationTokenType.MfaChallenge
                            && token.ConsumedAt == null
                            && token.ExpiresAt > now)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(token => token.ConsumedAt, now),
                cancellationToken);

        if (consumed == 0)
        {
            return null;
        }

        return await dbContext.VerificationTokens
            .AsNoTracking()
            .Where(token => token.TokenHash == hash)
            .Select(token => token.UserId)
            .SingleAsync(cancellationToken);
    }
}
