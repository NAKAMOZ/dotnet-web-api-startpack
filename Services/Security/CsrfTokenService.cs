using System.Security.Cryptography;
using System.Text;
using Api.Configuration;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;

namespace Api.Services.Security;

/// <summary>
/// Session-bound CSRF tokens: <c>base64url(nonce).base64url(tag)</c>, where the tag
/// authenticates the pair <c>(sessionId, nonce)</c> and expires with the session.
/// </summary>
/// <remarks>
/// <b>Why binding rather than plain double-submit.</b> Comparing the cookie to the header
/// proves only that whoever sent the request could read the cookie. An attacker who can
/// write a cookie for this site — a compromised sibling subdomain is enough, because cookies
/// ignore port and scheme boundaries — sets both halves to the same value of their choosing
/// and the comparison passes. Verifying the tag against the session the request authenticated
/// as closes that: a token minted for another session, or forged outright, fails even when
/// cookie and header agree (Authentication.md §3).
/// <para>
/// <b>Recorded deviation.</b> Authentication.md §3 writes the tag as
/// <c>HMAC(key, sessionId || nonce)</c>. This implementation produces the same authenticated
/// binding through an <see cref="ITimeLimitedDataProtector"/>, which is encrypt-then-MAC over
/// the same payload. The reason is key management, not cryptography: a raw HMAC needs a
/// secret that must be configured, distributed to every instance and rotated by hand, and the
/// obvious shortcut — generating one per process — silently breaks the moment a second
/// instance exists, in a way that looks like random CSRF failures under load. Data Protection
/// already provides a shared, rotating, ADR-0020-protected key ring, and it is what the
/// signing keys are protected with. The expiry is a free consequence.
/// </para>
/// </remarks>
public sealed class CsrfTokenService : ICsrfTokenService
{
    /// <summary>
    /// Purpose string for the protector. Isolates these tokens cryptographically from every
    /// other Data Protection consumer, so a token from one purpose cannot be unprotected by
    /// another even though both use the same key ring. The <c>.v1</c> suffix is the format
    /// version: changing it invalidates every outstanding token, which is the intended
    /// behaviour if the payload shape ever changes.
    /// </summary>
    private const string ProtectorPurpose = "Api.Security.CsrfToken.v1";

    /// <summary>256 bits of nonce. Unguessable is the requirement; collision is irrelevant.</summary>
    private const int NonceBytes = 32;

    private readonly ITimeLimitedDataProtector _protector;
    private readonly TimeProvider _timeProvider;
    private readonly TimeSpan _lifetime;

    public CsrfTokenService(
        IDataProtectionProvider dataProtectionProvider,
        IOptions<AuthSessionOptions> sessionOptions,
        TimeProvider timeProvider)
    {
        _protector = dataProtectionProvider.CreateProtector(ProtectorPurpose).ToTimeLimitedDataProtector();
        _timeProvider = timeProvider;

        // Tied to the absolute session cap, not to the inactivity window. A token that
        // outlives its session is harmless — the session id it is bound to no longer
        // authenticates anything — while one that expires sooner than the session produces
        // 403s in the middle of a live session, which clients "fix" by retrying blindly.
        _lifetime = sessionOptions.Value.AbsoluteLifetime;
    }

    public string Issue(Guid sessionId)
    {
        var nonce = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(NonceBytes));

        var tag = _protector.Protect(
            Encoding.UTF8.GetBytes(Payload(sessionId, nonce)),
            _timeProvider.GetUtcNow().Add(_lifetime));

        return $"{nonce}.{WebEncoders.Base64UrlEncode(tag)}";
    }

    public bool Validate(string? token, Guid sessionId)
    {
        if (string.IsNullOrEmpty(token))
        {
            return false;
        }

        var separator = token.IndexOf('.', StringComparison.Ordinal);

        if (separator <= 0 || separator == token.Length - 1)
        {
            return false;
        }

        var nonce = token[..separator];
        var tag = token[(separator + 1)..];

        try
        {
            var payload = _protector.Unprotect(WebEncoders.Base64UrlDecode(tag));

            // The tag is authentic, so its contents came from us. What is still open is
            // whether it was minted for THIS session and with THIS nonce — a token lifted
            // from another user's browser is perfectly authentic and must still fail.
            return CryptographicOperations.FixedTimeEquals(
                payload,
                Encoding.UTF8.GetBytes(Payload(sessionId, nonce)));
        }
        catch (Exception exception) when (exception is CryptographicException or FormatException)
        {
            // Tampered, truncated, expired, not base64url, or protected under a key that has
            // been revoked. Every one of them means the same thing to the caller: no.
            return false;
        }
    }

    /// <summary>
    /// The authenticated payload. The separator is what stops
    /// <c>(sessionId "ab", nonce "c")</c> and <c>(sessionId "a", nonce "bc")</c> from
    /// producing the same bytes — with a fixed-width <see cref="Guid"/> on the left it
    /// cannot happen today, and it stays true if the left side ever becomes variable.
    /// </summary>
    private static string Payload(Guid sessionId, string nonce) => $"{sessionId:N}:{nonce}";
}
