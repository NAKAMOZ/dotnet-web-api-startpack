using Api.Configuration;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.Extensions.Options;

namespace Api.Handlers.Cors;

/// <summary>
/// Chooses the CORS policy per request: credentials are offered to cookie-mode origins and
/// to nobody else (§14).
/// </summary>
/// <remarks>
/// <b>Why a provider instead of one policy.</b> <c>AllowCredentials</c> is a property of a
/// built <see cref="CorsPolicy"/>, not of an origin, so a single policy has to answer the
/// same way for every origin in its allowlist. Merging the two lists would therefore emit
/// <c>Access-Control-Allow-Credentials: true</c> to bearer-mode origins as well — harmless
/// on the day it ships, and precisely the header an XSS on one of those origins needs to
/// start making authenticated calls with the victim's cookies. Two policies and a per-request
/// choice keep the guarantee the roadmap asks for: credentials only for cookie-mode origins.
/// <para>
/// Registered in place of the framework's <c>DefaultCorsPolicyProvider</c>. Named policies
/// registered through <c>AddCors</c> are not consulted — this type is the only source of a
/// policy, which is what stops a second, looser one appearing elsewhere.
/// </para>
/// </remarks>
public sealed class OriginAwareCorsPolicyProvider : ICorsPolicyProvider
{
    private readonly ApiCorsOptions _options;
    private readonly CorsPolicy _credentialedPolicy;
    private readonly CorsPolicy _bearerPolicy;

    public OriginAwareCorsPolicyProvider(IOptions<ApiCorsOptions> options)
    {
        _options = options.Value;

        _credentialedPolicy = BuildPolicy(_options, _options.CookieModeOrigins, allowCredentials: true);

        // Both lists are allowed to call in bearer mode: an origin trusted with cookies is
        // trusted with an Authorization header. The reverse is not true, which is the whole
        // point of the split.
        _bearerPolicy = BuildPolicy(
            _options,
            [.. _options.AllowedOrigins, .. _options.CookieModeOrigins],
            allowCredentials: false);
    }

    public Task<CorsPolicy?> GetPolicyAsync(HttpContext context, string? policyName)
    {
        var origin = context.Request.Headers.Origin.ToString();

        // Ordinal, not case-insensitive. An origin is compared byte for byte by the browser
        // too, and a case-insensitive match here would accept an origin the browser then
        // refuses — a policy that appears to work and does not.
        var isCookieModeOrigin = !string.IsNullOrEmpty(origin)
            && _options.CookieModeOrigins.Contains(origin, StringComparer.Ordinal);

        return Task.FromResult<CorsPolicy?>(isCookieModeOrigin ? _credentialedPolicy : _bearerPolicy);
    }

    private static CorsPolicy BuildPolicy(ApiCorsOptions options, string[] origins, bool allowCredentials)
    {
        var builder = new CorsPolicyBuilder()
            .WithOrigins(origins)

            // Enumerated rather than AllowAnyMethod: the API has no PATCH and no TRACE, and
            // a method that is not in this list is one an attacker cannot reach through a
            // browser even if a route for it appears later.
            .WithMethods(HttpMethods.Get, HttpMethods.Post, HttpMethods.Put, HttpMethods.Delete, HttpMethods.Options)
            .WithHeaders(options.AllowedHeaders)
            .WithExposedHeaders(options.ExposedHeaders)
            .SetPreflightMaxAge(options.PreflightMaxAge);

        if (allowCredentials)
        {
            builder.AllowCredentials();
        }

        return builder.Build();
    }
}
