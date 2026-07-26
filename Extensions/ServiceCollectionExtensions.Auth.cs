using Api.Data;
using Api.Handlers.Authentication;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.DataProtection;

namespace Api.Extensions;

public static partial class ServiceCollectionExtensions
{
    /// <summary>Name of the policy scheme that routes each request to a concrete scheme.</summary>
    public const string CompositeSchemeName = "Composite";

    /// <summary>
    /// Data Protection isolation discriminator (ADR-0021). A fixed constant on purpose —
    /// this value is mixed into every purpose chain, so changing it makes every existing
    /// protected payload undecryptable, signing keys included.
    /// </summary>
    private const string DataProtectionApplicationName = "dotnet-web-api-startpack";

    /// <summary>
    /// Authentication schemes, token services, and authorization policies.
    /// </summary>
    public static IServiceCollection AddAuthenticationServices(this IServiceCollection services)
    {
        // Protects signing-key private material at rest (ADR-0020). Interim until a vault is
        // chosen (P7) — which is exactly why only ISigningKeyManager unprotects: the eventual
        // migration is then a change in one component.
        services
            .AddDataProtection()

            // Both lines are load-bearing (ADR-0021) and neither is a default worth trusting.
            //
            // SetApplicationName pins the isolation discriminator. Unset, it is derived from
            // the content root path — two instances deployed at different paths would share
            // the ring below and still fail to decrypt each other's payloads, which looks
            // exactly like the persistence not working. Never make this configurable:
            // changing it invalidates every existing payload, including every stored
            // signing key.
            .SetApplicationName(DataProtectionApplicationName)

            // Without persistence the ring is per-machine, and in a container with no
            // writable home directory it is per-process and in-memory, announced only as a
            // startup warning. Every restart would then orphan every SigningKey row.
            .PersistKeysToDbContext<AppDbContext>();

        services.ConfigureOptions<ConfigureJwtBearerOptions>();

        services
            .AddAuthentication(options =>
            {
                options.DefaultScheme = CompositeSchemeName;
                options.DefaultChallengeScheme = CompositeSchemeName;
            })

            // A policy scheme in front, choosing per request. Authorization policies stay
            // scheme-agnostic as a result: [RequirePermission] behaves identically whether
            // the caller arrived with a bearer token, an access cookie or an API key
            // (Authorization.md §9).
            .AddPolicyScheme(CompositeSchemeName, CompositeSchemeName, options =>
                options.ForwardDefaultSelector = context =>
                    context.Request.Headers.Authorization.ToString()
                        .Contains(ApiKeyAuthenticationHandler.KeyPrefix, StringComparison.Ordinal)
                        ? ApiKeyAuthenticationHandler.SchemeName
                        : JwtBearerDefaults.AuthenticationScheme)

            .AddScheme<AuthenticationSchemeOptions, ApiKeyAuthenticationHandler>(
                ApiKeyAuthenticationHandler.SchemeName,
                configureOptions: null)

            // Configured by ConfigureJwtBearerOptions, registered above.
            .AddJwtBearer();

        return services;
    }
}
