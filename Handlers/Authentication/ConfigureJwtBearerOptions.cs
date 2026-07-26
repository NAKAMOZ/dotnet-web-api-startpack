using Api.Configuration;
using Api.Services.Tokens;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Api.Handlers.Authentication;

/// <summary>
/// Configures JWT bearer validation: the ES256 pin, strict issuer and audience checks,
/// exact-match <c>kid</c> resolution, and cookie-borne token extraction.
/// </summary>
/// <remarks>
/// A configuration class rather than an inline lambda in the registration extension. The
/// lambda form needs a service provider to reach <see cref="ISigningKeyManager"/>, and the
/// usual way to get one there — calling <c>BuildServiceProvider()</c> during registration —
/// builds a <em>second</em> container with its own copies of every singleton. Two Data
/// Protection key rings would then exist, and keys protected by one would fail to unprotect
/// under the other.
/// </remarks>
public sealed class ConfigureJwtBearerOptions(
    IOptions<JwtOptions> jwtOptions,
    IOptions<AuthCookieOptions> cookieOptions,
    IServiceScopeFactory scopeFactory,
    TimeProvider timeProvider) : IConfigureNamedOptions<JwtBearerOptions>
{
    private readonly JwtOptions _jwt = jwtOptions.Value;
    private readonly AuthCookieOptions _cookies = cookieOptions.Value;

    public void Configure(string? name, JwtBearerOptions options)
    {
        if (name != JwtBearerDefaults.AuthenticationScheme)
        {
            return;
        }

        Configure(options);
    }

    public void Configure(JwtBearerOptions options)
    {
        // Claims are used exactly as the token spells them. The default mapping rewrites
        // `sub` into a long WS-Federation URI, which silently breaks every lookup written
        // against the claim name the token actually carries.
        options.MapInboundClaims = false;

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = _jwt.Issuer,

            ValidateAudience = true,
            ValidAudience = _jwt.Audience,

            ValidateLifetime = true,
            RequireExpirationTime = true,
            LifetimeValidator = ValidateLifetime,

            // 30 seconds, not the framework default of five minutes — which would extend
            // every access token's effective life by a third (Authentication.md §2).
            ClockSkew = _jwt.ClockSkew,

            // ── THE PIN ──────────────────────────────────────────────────────────────
            // The algorithm is never read from the token header to select a strategy.
            // This one line closes `alg: none` AND the HS256-using-the-public-key-as-HMAC-
            // secret attack — and it is the reason publishing JWKS anonymously is safe.
            // Removing it does not break any test that exists; it breaks the system.
            ValidateIssuerSigningKey = true,
            ValidAlgorithms = [_jwt.Algorithm],

            IssuerSigningKeyResolver = ResolveKeys,
        };

        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = OnMessageReceived,
            OnTokenValidated = OnTokenValidated,
        };
    }

    /// <summary>
    /// Exact-match <c>kid</c> resolution against <c>Active</c> and <c>Retiring</c> keys.
    /// </summary>
    /// <remarks>
    /// <b>There is no fallback, deliberately.</b> Returning the whole ring when a <c>kid</c>
    /// does not resolve looks like a robustness improvement and is the opposite: a retired
    /// key would keep validating, retirement would stop meaning anything, and a leaked old
    /// key would stay useful forever (Authentication.md §12). §22 tests both the unknown-kid
    /// and retired-kid cases for exactly this reason.
    /// </remarks>
    private IEnumerable<SecurityKey> ResolveKeys(
        string token,
        SecurityToken securityToken,
        string? keyId,
        TokenValidationParameters parameters)
    {
        if (string.IsNullOrEmpty(keyId))
        {
            return [];
        }

        using var scope = scopeFactory.CreateScope();
        var keyManager = scope.ServiceProvider.GetRequiredService<ISigningKeyManager>();

        // Synchronous by the resolver's contract. The lookup is a single indexed read; §17's
        // caching pass is where it stops touching the database per request.
        var ecdsa = keyManager
            .ResolveValidationKeyAsync(keyId, CancellationToken.None)
            .GetAwaiter()
            .GetResult();

        return ecdsa is null ? [] : [new ECDsaSecurityKey(ecdsa) { KeyId = keyId }];
    }

    private Task OnMessageReceived(MessageReceivedContext context)
    {
        // Cookie mode: the access token lives in __Host-auth.access. Read only when no
        // bearer header was supplied, so a caller presenting both cannot have the ambient
        // cookie silently win over the credential they chose.
        if (string.IsNullOrEmpty(context.Token)
            && context.Request.Cookies.TryGetValue(_cookies.AccessCookieName, out var cookieToken))
        {
            context.Token = cookieToken;

            // The credential is ambient from here on, which is the definition of a request
            // reachable by CSRF. §14's filter enforces the token on exactly these requests
            // and exempts the bearer ones — see AuthTransport for why the marker is set here
            // rather than inferred later.
            context.HttpContext.Items[AuthTransport.CookieAuthenticatedItemKey] = true;
        }

        return Task.CompletedTask;
    }

    private bool ValidateLifetime(
        DateTime? notBefore,
        DateTime? expires,
        SecurityToken token,
        TokenValidationParameters parameters)
    {
        _ = token;

        if (expires is null || (notBefore is not null && notBefore > expires))
        {
            return false;
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;

        return (notBefore is null || notBefore <= now + parameters.ClockSkew)
               && expires >= now - parameters.ClockSkew;
    }

    private static Task OnTokenValidated(TokenValidatedContext context)
    {
        // Rejects a refresh token presented as a bearer token. Without this check, any
        // opaque credential that happened to parse as a JWT would be judged on its claims
        // alone (Authentication.md §2).
        if (context.Principal?.FindFirst("token_use")?.Value != "access")
        {
            context.Fail("Token is not an access token.");
        }

        return Task.CompletedTask;
    }
}
