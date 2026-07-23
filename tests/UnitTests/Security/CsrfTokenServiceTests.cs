using Api.Configuration;
using Api.Services.Security;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace UnitTests.Security;

/// <summary>
/// The property that makes this CSRF scheme worth more than a plain double submit: the token
/// is bound to one session (§14, Authentication.md §3).
/// </summary>
public class CsrfTokenServiceTests
{
    private static readonly AuthSessionOptions SessionOptions = new();

    [Fact]
    public void AnIssuedTokenValidatesForItsOwnSession()
    {
        var sessionId = Guid.NewGuid();
        var service = CreateService(out _);

        Assert.True(service.Validate(service.Issue(sessionId), sessionId));
    }

    [Fact]
    public void ATokenMintedForAnotherSessionIsRejected()
    {
        // The assertion the whole design exists for. An attacker who can set a cookie for
        // this origin can make the double-submit comparison pass with a token they hold —
        // their own, from their own session. The binding is what stops that token from
        // authorising anything in the victim's session.
        var service = CreateService(out _);

        var attackerToken = service.Issue(Guid.NewGuid());

        Assert.False(service.Validate(attackerToken, Guid.NewGuid()));
    }

    [Theory]
    [InlineData("")]
    [InlineData("no-separator")]
    [InlineData(".")]
    [InlineData("nonce.")]
    [InlineData(".tag")]
    [InlineData("nonce.not-base64url!!")]
    public void MalformedTokensAreRejectedWithoutThrowing(string token)
    {
        // A filter that throws on a malformed token turns a forged request into a 500 and,
        // in Development, into a stack trace describing the token format.
        var service = CreateService(out _);

        Assert.False(service.Validate(token, Guid.NewGuid()));
    }

    [Fact]
    public void ATamperedTokenIsRejected()
    {
        var sessionId = Guid.NewGuid();
        var service = CreateService(out _);

        var token = service.Issue(sessionId);

        // Swap the nonce half while keeping the authentic tag. The tag authenticates the
        // pair, so the halves no longer agree and the payload comparison fails.
        var forged = $"{service.Issue(sessionId).Split('.')[0]}.{token.Split('.')[1]}";

        Assert.False(service.Validate(forged, sessionId));
    }

    [Fact]
    public void AnExpiredTokenIsRejected()
    {
        var sessionId = Guid.NewGuid();

        // Issued far enough in the past that its expiry — issue time plus the absolute
        // session lifetime — is already behind the real clock the protector checks against.
        // The service's clock is injected; the protector's expiry check uses the system one,
        // which is what makes this assertion meaningful rather than circular.
        var service = CreateService(
            out _,
            issuedAt: DateTimeOffset.UtcNow - SessionOptions.AbsoluteLifetime - TimeSpan.FromDays(1));

        Assert.False(service.Validate(service.Issue(sessionId), sessionId));
    }

    [Fact]
    public void TokensIssuedForTheSameSessionDiffer()
    {
        // Each token carries its own nonce, so a token captured from one response is not the
        // token the next request will be checked against.
        var sessionId = Guid.NewGuid();
        var service = CreateService(out _);

        Assert.NotEqual(service.Issue(sessionId), service.Issue(sessionId));
    }

    private static CsrfTokenService CreateService(out FakeTimeProvider timeProvider, DateTimeOffset? issuedAt = null)
    {
        // An ephemeral key ring: keys live in this process only, which is exactly right for
        // a unit test and exactly wrong for the application — see the class remarks on
        // CsrfTokenService for why the real one is Data Protection's shared ring.
        timeProvider = new FakeTimeProvider(issuedAt ?? DateTimeOffset.UtcNow);

        return new CsrfTokenService(
            new EphemeralDataProtectionProvider(),
            Options.Create(SessionOptions),
            timeProvider);
    }
}
