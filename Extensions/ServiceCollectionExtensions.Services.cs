using Api.BackgroundServices;
using Api.Configuration;
using Api.Services.ApiKeys;
using Api.Services.Audit;
using Api.Services.Auth;
using Api.Services.Crypto;
using Api.Services.Email;
using Api.Services.Mfa;
using Api.Services.Operations;
using Api.Services.Passkeys;
using Api.Services.Security;
using Api.Services.Sessions;
using Api.Services.SocialAuth;
using Api.Services.Tokens;
using Api.Services.Users;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Api.Extensions;

public static partial class ServiceCollectionExtensions
{
    /// <summary>
    /// Domain services — crypto primitives, the token pipeline, and feature services.
    /// </summary>
    public static IServiceCollection AddDomainServices(
        this IServiceCollection services,
        IConfiguration configuration)
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
        services.AddScoped<IRegistrationService, RegistrationService>();
        services.AddScoped<IEmailVerificationService, EmailVerificationService>();
        services.AddSingleton<DummyPasswordHash>();
        services.AddScoped<IAuthTokenTransport, AuthTokenTransport>();
        services.AddScoped<IAuthenticationSessionFactory, AuthenticationSessionFactory>();
        services.AddScoped<ILoginService, LoginService>();
        services.AddScoped<IRefreshService, RefreshService>();
        services.AddScoped<ILogoutService, LogoutService>();
        services.AddScoped<IPasswordResetService, PasswordResetService>();
        services.AddScoped<ITotpService, TotpService>();
        services.AddScoped<IRecoveryCodeService, RecoveryCodeService>();
        services.AddScoped<ISessionQueryService, SessionQueryService>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IAdminUserService, AdminUserService>();
        services.AddScoped<IAdminRoleService, AdminRoleService>();
        services.AddScoped<IAdminSessionService, AdminSessionService>();
        services.AddScoped<IApiKeyService, ApiKeyService>();
        services.AddScoped<ISocialAuthService, SocialAuthService>();
        services.AddScoped<IPasskeyService, PasskeyService>();
        services.AddScoped<DatabaseDeploymentService>();
        services.AddHttpClient();
        var webAuthn = configuration
            .GetSection(WebAuthnOptions.SectionName)
            .Get<WebAuthnOptions>() ?? new WebAuthnOptions();
        services.AddFido2(options =>
        {
            options.ServerDomain = webAuthn.ServerDomain;
            options.ServerName = webAuthn.ServerName;
            options.Origins = webAuthn.Origins.ToHashSet(StringComparer.Ordinal);
        });

        services.AddSingleton<EmbeddedEmailTemplateRenderer>();
        services.AddSingleton<IEmailTemplateRenderer>(
            static provider => provider.GetRequiredService<EmbeddedEmailTemplateRenderer>());
        services.AddScoped<ISecurityNotificationService, SecurityNotificationService>();
        services.AddSingleton<SmtpEmailSender>();
        services.AddSingleton<IEmailSender>(
            static provider => provider.GetRequiredService<SmtpEmailSender>());
        services.AddHostedService(
            static provider => provider.GetRequiredService<SmtpEmailSender>());
        services.AddHostedService<ExpiredAuthArtifactCleanupService>();

        // Singleton, and the lifetime is the design (§15). AuditLogger writes through a scope
        // it creates itself rather than the request's, so it holds no scoped dependency —
        // making it singleton is what stops one being added later without noticing.
        services.AddSingleton<IAuditLogger, AuditLogger>();

        // Scoped: it reads through the request's AppDbContext, like every other query path.
        services.AddScoped<IAuditQueryService, AuditQueryService>();

        // Singleton — it holds options and a clock, and mutates the User it is handed rather
        // than reading one (§16). The login service that calls it is scoped; this is not.
        services.AddSingleton<LockoutPolicy>();

        return services;
    }
}
