using Api.Handlers.Authentication;
using Microsoft.AspNetCore.Authentication;

namespace Api.Extensions;

public static partial class ServiceCollectionExtensions
{
    /// <summary>
    /// Authentication schemes, token services, and authorization policies.
    /// </summary>
    public static IServiceCollection AddAuthenticationServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // TODO §4/§12: JwtBearer with alg pinned to ES256 (ADR-0001, ADR-0004);
        //              cookie and API-key authentication handlers (ADR-0003);
        //              IAccessTokenIssuer, IRefreshTokenService, ISigningKeyManager.

        // ┌── TEMPORARY (§11) — REPLACE IN §12 ────────────────────────────────────────┐
        // │  A scheme that authenticates nobody, registered as the default so that     │
        // │  [Authorize] endpoints can be CHALLENGED rather than throwing.             │
        // └────────────────────────────────────────────────────────────────────────────┘
        //
        // Without a default challenge scheme, an [Authorize] endpoint does not return 401 —
        // it throws InvalidOperationException, so every protected route answers 500. The
        // controllers in §11 would then be unroutable in practice, and an authorization
        // failure would be indistinguishable from a server fault.
        //
        // This handler cannot authenticate anyone (it always returns NoResult), so the
        // registration is fail-closed: if it survives into §12, the symptom is that nobody
        // can log in anywhere, which is impossible to miss.
        services
            .AddAuthentication(PlaceholderAuthenticationHandler.SchemeName)
            .AddScheme<AuthenticationSchemeOptions, PlaceholderAuthenticationHandler>(
                PlaceholderAuthenticationHandler.SchemeName,
                configureOptions: null);

        return services;
    }
}
