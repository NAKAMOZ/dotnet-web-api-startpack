using Api.Services.Crypto;
using Api.Services.Tokens;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Api.Extensions;

public static partial class ServiceCollectionExtensions
{
    /// <summary>
    /// Domain services — crypto primitives, the token pipeline, and feature services.
    /// </summary>
    public static IServiceCollection AddDomainServices(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);

        // Stateless and cheap to construct; the cost is in the algorithm, not the object.
        services.AddSingleton<ITokenGenerator, TokenGenerator>();
        services.AddSingleton<IPasswordHasher, Argon2PasswordHasher>();

        // Scoped — each takes AppDbContext, whose lifetime is the request.
        //
        // The roadmap suggests a singleton key manager with a cache. It is scoped here
        // instead, because a singleton holding a DbContext is a captive dependency: the
        // context outlives the request, accumulates tracked entities, and is not
        // thread-safe. Caching belongs on the data it returns, not on the service — §17.
        services.AddScoped<ISigningKeyManager, SigningKeyManager>();
        services.AddScoped<IAccessTokenIssuer, AccessTokenIssuer>();
        services.AddScoped<ISessionService, SessionService>();
        services.AddScoped<IRefreshTokenService, RefreshTokenService>();
        services.AddScoped<IMfaTicketService, MfaTicketService>();

        // TODO §12 (remaining): Services/Auth, Services/Users, Services/Mfa,
        //                       Services/Passkeys, Services/SocialAuth, Services/ApiKeys,
        //                       Services/Email, Services/Audit.

        return services;
    }
}
