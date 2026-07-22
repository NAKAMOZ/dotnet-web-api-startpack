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
        // TODO §5:     policy-based authorization over the static permission map.
        return services;
    }
}
