using Api.Configuration;
using Api.Configuration.Validation;
using Microsoft.Extensions.Options;

namespace Api.Extensions;

public static partial class ServiceCollectionExtensions
{
    /// <summary>
    /// Binds and validates every operator-controlled setting in one discoverable place.
    /// </summary>
    /// <remarks>
    /// Every registration uses <c>ValidateOnStart</c>. An invalid deployment therefore
    /// fails before it accepts traffic instead of discovering the setting on the first
    /// authentication request.
    /// <para>
    /// One line per options type, binding and validator together. Two parallel lists would
    /// let a validator be registered against a section nothing binds — which fails silently,
    /// because <c>ValidateOnStart</c> only ever runs the validators of options it was asked
    /// to bind.
    /// </para>
    /// </remarks>
    public static IServiceCollection AddValidatedOptions(this IServiceCollection services)
    {
        AddValidatedSection<JwtOptions, JwtOptionsValidator>(services, JwtOptions.SectionName);
        AddValidatedSection<AuthSessionOptions, AuthSessionOptionsValidator>(
            services,
            AuthSessionOptions.SectionName);
        AddValidatedSection<AuthCookieOptions, AuthCookieOptionsValidator>(
            services,
            AuthCookieOptions.SectionName);
        AddValidatedSection<ApiCorsOptions, ApiCorsOptionsValidator>(
            services,
            ApiCorsOptions.SectionName);
        AddValidatedSection<SocialProviderOptions, SocialProviderOptionsValidator>(
            services,
            SocialProviderOptions.SectionName);
        AddValidatedSection<ReverseProxyOptions, ReverseProxyOptionsValidator>(
            services,
            ReverseProxyOptions.SectionName);
        AddValidatedSection<TelemetryOptions, TelemetryOptionsValidator>(
            services,
            TelemetryOptions.SectionName);
        AddValidatedSection<WebAuthnOptions, WebAuthnOptionsValidator>(
            services,
            WebAuthnOptions.SectionName);
        AddValidatedSection<AzurePlatformOptions, AzurePlatformOptionsValidator>(
            services,
            AzurePlatformOptions.SectionName);
        AddValidatedSection<RedisOptions, RedisOptionsValidator>(
            services,
            RedisOptions.SectionName);
        AddValidatedSection<EmailOptions, EmailOptionsValidator>(
            services,
            EmailOptions.SectionName);

        // Data annotations carry the whole contract for these; no validator class needed.
        AddValidatedSection<PasswordHashingOptions>(services, PasswordHashingOptions.SectionName);
        AddValidatedSection<LockoutOptions>(services, LockoutOptions.SectionName);
        AddValidatedSection<RateLimitOptions>(services, RateLimitOptions.SectionName);
        AddValidatedSection<CleanupOptions>(services, CleanupOptions.SectionName);
        AddValidatedSection<RequestSecurityOptions>(services, RequestSecurityOptions.SectionName);

        return services;
    }

    private static void AddValidatedSection<TOptions, TValidator>(
        IServiceCollection services,
        string sectionName)
        where TOptions : class
        where TValidator : class, IValidateOptions<TOptions>
    {
        services.AddSingleton<IValidateOptions<TOptions>, TValidator>();
        AddValidatedSection<TOptions>(services, sectionName);
    }

    private static void AddValidatedSection<TOptions>(IServiceCollection services, string sectionName)
        where TOptions : class =>
        services
            .AddOptions<TOptions>()
            .BindConfiguration(sectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();
}
