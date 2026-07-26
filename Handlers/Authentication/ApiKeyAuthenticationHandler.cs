using System.Security.Claims;
using System.Text.Encodings.Web;
using Api.Data;
using Api.Services.Crypto;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Api.Handlers.Authentication;

/// <summary>
/// Authenticates <c>ak_&lt;prefix&gt;_&lt;secret&gt;</c> credentials (Authentication.md §15).
/// </summary>
/// <remarks>
/// API keys are a parallel path, not a session: they create no session, take no part in
/// refresh, and carry no <c>auth_time</c> — so they can never satisfy step-up, because no
/// human authenticated.
/// </remarks>
public sealed class ApiKeyAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    AppDbContext dbContext,
    IPasswordHasher passwordHasher,
    TimeProvider timeProvider) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "ApiKey";

    /// <summary>Every key starts with this. Also how the scheme selector recognises one.</summary>
    public const string KeyPrefix = "ak_";

    /// <summary>Claim carrying one granted scope. Repeated once per scope.</summary>
    public const string ScopeClaimType = "scope";

    /// <summary>Marks a principal as key-authenticated, so step-up can refuse it outright.</summary>
    public const string ApiKeyIdClaimType = "api_key_id";

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!TryReadCredential(out var presented))
        {
            // NoResult, not Fail: this request simply was not trying to use an API key, and
            // failing would veto the other schemes in the policy selector.
            return AuthenticateResult.NoResult();
        }

        // ak_<prefix>_<secret> — split from the left so a secret containing an underscore
        // cannot shift the boundary.
        var segments = presented.Split('_', 3);

        if (segments.Length != 3)
        {
            return AuthenticateResult.Fail("Malformed API key.");
        }

        var (prefix, secret) = (segments[1], segments[2]);
        var now = timeProvider.GetUtcNow();

        // One indexed lookup on the plaintext prefix, then one hash verification. Verifying
        // against every row instead would make each request a table scan of deliberately
        // expensive work.
        var key = await dbContext.ApiKeys
            .AsNoTracking()
            .Include(candidate => candidate.User)
            .SingleOrDefaultAsync(candidate => candidate.KeyPrefix == prefix);

        if (key is null || key.RevokedAt is not null || (key.ExpiresAt is not null && key.ExpiresAt <= now))
        {
            return AuthenticateResult.Fail("Invalid API key.");
        }

        if (!passwordHasher.Verify(secret, key.KeyHash))
        {
            return AuthenticateResult.Fail("Invalid API key.");
        }

        await dbContext.ApiKeys
            .Where(candidate => candidate.Id == key.Id)
            .ExecuteUpdateAsync(setters => setters.SetProperty(candidate => candidate.LastUsedAt, now));

        var roles = await dbContext.UserRoles
            .AsNoTracking()
            .Where(userRole => userRole.UserId == key.UserId)
            .Select(userRole => userRole.Role.Name)
            .ToListAsync();

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, key.UserId.ToString()),
            new(ApiKeyIdClaimType, key.Id.ToString()),
        };

        claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));

        // The key's own scopes are carried as claims, but they are NOT the effective
        // permission set on their own: PermissionAuthorizationHandler intersects them with
        // the owner's role-granted permissions at request time, so a key cannot outlive its
        // creator's authority (Authorization.md §7).
        claims.AddRange(key.Scopes.Select(scope => new Claim(ScopeClaimType, scope)));

        // Deliberately absent: auth_time. Its absence is what makes step-up impossible to
        // satisfy with a key, and that is a feature — no human authenticated.
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, SchemeName));

        return AuthenticateResult.Success(new AuthenticationTicket(principal, SchemeName));
    }

    private bool TryReadCredential(out string credential)
    {
        credential = string.Empty;

        var header = Request.Headers.Authorization.ToString();

        if (string.IsNullOrEmpty(header))
        {
            return false;
        }

        // Accepts both "ApiKey ak_…" and a bare "ak_…", because clients send both and
        // rejecting one produces a 401 that looks like a wrong key rather than a wrong shape.
        var value = header.StartsWith($"{SchemeName} ", StringComparison.OrdinalIgnoreCase)
            ? header[(SchemeName.Length + 1)..]
            : header;

        if (!value.StartsWith(KeyPrefix, StringComparison.Ordinal))
        {
            return false;
        }

        credential = value.Trim();
        return true;
    }
}
