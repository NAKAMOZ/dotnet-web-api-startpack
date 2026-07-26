using Api.Configuration;
using Api.Configuration.Validation;

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
    /// </remarks>
    public static IServiceCollection AddValidatedOptions(this IServiceCollection services)
    {
        services.AddSingleton<Microsoft.Extensions.Options.IValidateOptions<JwtOptions>, JwtOptionsValidator>();
        services.AddSingleton<
            Microsoft.Extensions.Options.IValidateOptions<AuthSessionOptions>,
            AuthSessionOptionsValidator>();
        services.AddSingleton<
            Microsoft.Extensions.Options.IValidateOptions<AuthCookieOptions>,
            AuthCookieOptionsValidator>();
        services.AddSingleton<
            Microsoft.Extensions.Options.IValidateOptions<ApiCorsOptions>,
            ApiCorsOptionsValidator>();
        services.AddSingleton<
            Microsoft.Extensions.Options.IValidateOptions<SocialProviderOptions>,
            SocialProviderOptionsValidator>();
        services.AddSingleton<
            Microsoft.Extensions.Options.IValidateOptions<ReverseProxyOptions>,
            ReverseProxyOptionsValidator>();
        services.AddSingleton<
            Microsoft.Extensions.Options.IValidateOptions<TelemetryOptions>,
            TelemetryOptionsValidator>();

        AddValidatedSection<JwtOptions>(services, JwtOptions.SectionName);
        AddValidatedSection<AuthSessionOptions>(services, AuthSessionOptions.SectionName);
        AddValidatedSection<AuthCookieOptions>(services, AuthCookieOptions.SectionName);
        AddValidatedSection<PasswordHashingOptions>(services, PasswordHashingOptions.SectionName);
        AddValidatedSection<LockoutOptions>(services, LockoutOptions.SectionName);
        AddValidatedSection<RateLimitOptions>(services, RateLimitOptions.SectionName);
        AddValidatedSection<EmailOptions>(services, EmailOptions.SectionName);
        AddValidatedSection<SocialProviderOptions>(services, SocialProviderOptions.SectionName);
        AddValidatedSection<ApiCorsOptions>(services, ApiCorsOptions.SectionName);
        AddValidatedSection<CleanupOptions>(services, CleanupOptions.SectionName);
        AddValidatedSection<ReverseProxyOptions>(services, ReverseProxyOptions.SectionName);
        AddValidatedSection<TelemetryOptions>(services, TelemetryOptions.SectionName);

        services
            .AddOptions<AuthCookieOptions>()
            .Validate<IHostEnvironment>(
                (options, environment) =>
                    options.RequireSecure
                    || environment.IsDevelopment()
                    || environment.IsEnvironment("Testing"),
                "AuthCookies:RequireSecure must be true outside Development and Testing.");

        return services;
    }

    private static void AddValidatedSection<TOptions>(IServiceCollection services, string sectionName)
        where TOptions : class =>
        services
            .AddOptions<TOptions>()
            .BindConfiguration(sectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();
}
