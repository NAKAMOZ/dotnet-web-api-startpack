using Api.Configuration;
using Api.Configuration.Validation;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace UnitTests.Configuration;

public sealed class OptionsValidationTests
{
    [Fact]
    public void Jwt_GraceShorterThanTokenLifetime_IsRejected()
    {
        var result = new JwtOptionsValidator(Environment(Environments.Development)).Validate(
            null,
            new JwtOptions
            {
                Issuer = "https://issuer.example",
                Audience = "api",
                AccessTokenLifetime = TimeSpan.FromMinutes(15),
                ClockSkew = TimeSpan.FromSeconds(30),
                KeyRetirementGrace = TimeSpan.FromMinutes(15),
            });

        OptionsAssert.Failed(result);
        Assert.Contains("KeyRetirementGrace", result.FailureMessage);
    }

    [Fact]
    public void Jwt_AlgorithmOtherThanEs256_IsRejected()
    {
        var result = new JwtOptionsValidator(Environment(Environments.Development)).Validate(
            null,
            new JwtOptions
            {
                Issuer = "https://issuer.example",
                Audience = "api",
                Algorithm = "HS256",
            });

        OptionsAssert.Failed(result);
        Assert.Contains("ES256", result.FailureMessage);
    }

    [Fact]
    public void Session_InactivityAtOrBeyondAbsoluteCap_IsRejected()
    {
        var result = SessionValidator().Validate(
            null,
            new AuthSessionOptions
            {
                InactivityWindow = TimeSpan.FromDays(7),
                AbsoluteLifetime = TimeSpan.FromDays(7),
            });

        OptionsAssert.Failed(result);
        Assert.Contains("InactivityWindow", result.FailureMessage);
    }

    [Fact]
    public void Session_AccessTokenAtOrBeyondInactivityWindow_IsRejected()
    {
        var result = SessionValidator(TimeSpan.FromMinutes(15)).Validate(
            null,
            new AuthSessionOptions
            {
                InactivityWindow = TimeSpan.FromMinutes(15),
            });

        OptionsAssert.Failed(result);
        Assert.Contains("AccessTokenLifetime", result.FailureMessage);
    }

    [Fact]
    public void Cookie_UnsafeNamesAndRootScopedRefresh_AreRejected()
    {
        var result = new AuthCookieOptionsValidator(Environment(Environments.Development)).Validate(
            null,
            new AuthCookieOptions
            {
                AccessCookieName = "auth.access",
                RefreshCookiePath = "/",
            });

        OptionsAssert.Failed(result);
    }

    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    public void Cookie_InsecureOutsideDevelopmentAndTesting_IsRejected(string environmentName)
    {
        var result = new AuthCookieOptionsValidator(Environment(environmentName)).Validate(
            null,
            new AuthCookieOptions { RequireSecure = false });

        OptionsAssert.Failed(result);
        Assert.Contains("RequireSecure", result.FailureMessage);
    }

    [Theory]
    [InlineData("Development")]
    [InlineData("Testing")]
    public void Cookie_InsecureInDevelopmentOrTesting_IsAccepted(string environmentName)
    {
        OptionsAssert.Succeeded(
            new AuthCookieOptionsValidator(Environment(environmentName)).Validate(
                null,
                new AuthCookieOptions { RequireSecure = false }));
    }

    [Fact]
    public void Cors_WildcardOrPathOrigin_IsRejected()
    {
        var validator = new ApiCorsOptionsValidator();

        OptionsAssert.Failed(validator.Validate(
            null,
            new ApiCorsOptions { AllowedOrigins = ["*"] }));
        OptionsAssert.Failed(validator.Validate(
            null,
            new ApiCorsOptions { AllowedOrigins = ["https://app.example/path"] }));
    }

    [Fact]
    public void SocialProvider_EnabledWithoutBothCredentials_IsRejected()
    {
        var result = new SocialProviderOptionsValidator().Validate(
            null,
            new SocialProviderOptions
            {
                Google = new SocialProviderOptions.Provider
                {
                    Enabled = true,
                    ClientId = "client-id",
                },
            });

        OptionsAssert.Failed(result);
        Assert.Contains("ClientSecret", result.FailureMessage);
    }

    [Fact]
    public void ReverseProxy_ProductionWithoutExplicitTrustBoundary_IsRejected()
    {
        var result = new ReverseProxyOptionsValidator(Environment(Environments.Production)).Validate(
            null,
            new ReverseProxyOptions());

        OptionsAssert.Failed(result);
        Assert.Contains("Enabled", result.FailureMessage);
    }

    [Fact]
    public void ReverseProxy_InvalidProxyAndNetwork_AreRejected()
    {
        var result = new ReverseProxyOptionsValidator(Environment(Environments.Production)).Validate(
            null,
            new ReverseProxyOptions
            {
                Enabled = true,
                KnownProxies = ["not-an-ip"],
                KnownNetworks = ["10.0.0.1/not-a-prefix"],
            });

        OptionsAssert.Failed(result);
        Assert.Contains("invalid IP", result.FailureMessage);
        Assert.Contains("invalid CIDR", result.FailureMessage);
    }

    [Fact]
    public void Telemetry_EnabledExporterWithoutEndpoint_IsRejected()
    {
        var result = new TelemetryOptionsValidator().Validate(
            null,
            new TelemetryOptions { OtlpExporterEnabled = true });

        OptionsAssert.Failed(result);
        Assert.Contains("OtlpEndpoint", result.FailureMessage);
    }

    [Fact]
    public void Telemetry_AzureMonitorWithoutConnectionString_IsRejected()
    {
        var result = new TelemetryOptionsValidator().Validate(
            null,
            new TelemetryOptions { AzureMonitorExporterEnabled = true });

        OptionsAssert.Failed(result);
        Assert.Contains("AzureMonitorConnectionString", result.FailureMessage);
    }

    [Fact]
    public void WebAuthn_CrossDomainOrInsecureOrigin_IsRejected()
    {
        var validator = new WebAuthnOptionsValidator(Environment(Environments.Development));

        OptionsAssert.Failed(validator.Validate(
            null,
            new WebAuthnOptions
            {
                ServerDomain = "auth.example.com",
                Origins = ["https://attacker.example"],
            }));
        OptionsAssert.Failed(validator.Validate(
            null,
            new WebAuthnOptions
            {
                ServerDomain = "auth.example.com",
                Origins = ["http://auth.example.com"],
            }));
    }

    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    public void AzurePlatform_ProductionLikeRequiresVersionlessKeyVaultKey(string environmentName)
    {
        var validator = new AzurePlatformOptionsValidator(Environment(environmentName));

        OptionsAssert.Failed(validator.Validate(null, new AzurePlatformOptions()));
        OptionsAssert.Failed(validator.Validate(
            null,
            new AzurePlatformOptions
            {
                DataProtectionKeyIdentifier = new Uri(
                    "https://vault.vault.azure.net/keys/data-protection/version"),
            }));
        OptionsAssert.Succeeded(validator.Validate(
            null,
            new AzurePlatformOptions
            {
                DataProtectionKeyIdentifier = new Uri(
                    "https://vault.vault.azure.net/keys/data-protection"),
            }));
    }

    [Fact]
    public void Redis_EnabledWithoutEndpointOrProductionIdentity_IsRejected()
    {
        var development = new RedisOptionsValidator(Environment(Environments.Development));
        var production = new RedisOptionsValidator(Environment(Environments.Production));

        OptionsAssert.Failed(development.Validate(null, new RedisOptions { Enabled = true }));
        OptionsAssert.Failed(production.Validate(
            null,
            new RedisOptions
            {
                Enabled = true,
                Endpoint = "cache.example:10000",
            }));
        OptionsAssert.Succeeded(production.Validate(
            null,
            new RedisOptions
            {
                Enabled = true,
                Endpoint = "cache.example:10000",
                UseAzureIdentity = true,
            }));
    }

    [Fact]
    public void ProductionLike_RejectsLoopbackJwtAndWebAuthnSettings()
    {
        var staging = Environment("Staging");

        OptionsAssert.Failed(new JwtOptionsValidator(staging).Validate(
            null,
            new JwtOptions
            {
                Issuer = "https://localhost:7052",
                Audience = "api",
            }));
        OptionsAssert.Failed(new WebAuthnOptionsValidator(staging).Validate(
            null,
            new WebAuthnOptions()));
    }

    [Fact]
    public void Email_PartialCredentialsOrInsecureProductionLikeSmtp_AreRejected()
    {
        var development = new EmailOptionsValidator(Environment(Environments.Development));
        var staging = new EmailOptionsValidator(Environment("Staging"));

        OptionsAssert.Failed(development.Validate(
            null,
            new EmailOptions { Username = "smtp-user" }));
        OptionsAssert.Failed(staging.Validate(null, new EmailOptions()));
        OptionsAssert.Succeeded(staging.Validate(
            null,
            new EmailOptions
            {
                Host = "smtp.example.com",
                Port = 587,
                FromAddress = "auth@example.com",
                UseTls = true,
                Username = "smtp-user",
                Password = "smtp-password",
            }));
    }

    [Fact]
    public void CrossFieldDefaults_AreValid()
    {
        var development = Environment(Environments.Development);
        OptionsAssert.Succeeded(new JwtOptionsValidator(development).Validate(
            null,
            new JwtOptions
            {
                Issuer = "https://issuer.example",
                Audience = "api",
            }));
        OptionsAssert.Succeeded(SessionValidator().Validate(null, new AuthSessionOptions()));
        OptionsAssert.Succeeded(new AuthCookieOptionsValidator(development).Validate(
            null,
            new AuthCookieOptions()));
        OptionsAssert.Succeeded(new ApiCorsOptionsValidator().Validate(null, new ApiCorsOptions()));
        OptionsAssert.Succeeded(new SocialProviderOptionsValidator().Validate(null, new SocialProviderOptions()));
        OptionsAssert.Succeeded(new ReverseProxyOptionsValidator(development).Validate(
            null,
            new ReverseProxyOptions()));
        OptionsAssert.Succeeded(new TelemetryOptionsValidator().Validate(null, new TelemetryOptions()));
        OptionsAssert.Succeeded(new WebAuthnOptionsValidator(development).Validate(
            null,
            new WebAuthnOptions()));
        OptionsAssert.Succeeded(new AzurePlatformOptionsValidator(development).Validate(
            null,
            new AzurePlatformOptions()));
        OptionsAssert.Succeeded(new RedisOptionsValidator(development).Validate(
            null,
            new RedisOptions()));
        OptionsAssert.Succeeded(new EmailOptionsValidator(development).Validate(
            null,
            new EmailOptions()));
    }

    private static IHostEnvironment Environment(string environmentName)
    {
        var environment = Substitute.For<IHostEnvironment>();
        environment.EnvironmentName.Returns(environmentName);
        return environment;
    }

    private static AuthSessionOptionsValidator SessionValidator(
        TimeSpan? accessTokenLifetime = null) =>
        new(Options.Create(new JwtOptions
        {
            Issuer = "https://issuer.example",
            Audience = "api",
            AccessTokenLifetime = accessTokenLifetime ?? TimeSpan.FromMinutes(15),
        }));
}
