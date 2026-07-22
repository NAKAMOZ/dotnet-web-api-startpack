using Api.Configuration;
using Api.Handlers.Authentication;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;

namespace Api.Extensions;

public static partial class ServiceCollectionExtensions
{
    /// <summary>Name of the policy scheme that routes each request to a concrete scheme.</summary>
    public const string CompositeSchemeName = "Composite";

    /// <summary>
    /// Authentication schemes, token services, and authorization policies.
    /// </summary>
    public static IServiceCollection AddAuthenticationServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services
            .AddOptions<JwtOptions>()
            .BindConfiguration(JwtOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services
            .AddOptions<AuthCookieOptions>()
            .BindConfiguration(AuthCookieOptions.SectionName)
            .ValidateOnStart();

        services
            .AddOptions<PasswordHashingOptions>()
            .BindConfiguration(PasswordHashingOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        // Protects signing-key private material at rest (ADR-0020). Interim until a vault is
        // chosen (P7) — which is exactly why only ISigningKeyManager unprotects: the eventual
        // migration is then a change in one component.
        services.AddDataProtection();

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
