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
        var result = new JwtOptionsValidator().Validate(
            null,
            new JwtOptions
            {
                Issuer = "https://issuer.example",
                Audience = "api",
                AccessTokenLifetime = TimeSpan.FromMinutes(15),
                ClockSkew = TimeSpan.FromSeconds(30),
                KeyRetirementGrace = TimeSpan.FromMinutes(15),
            });

        Assert.Failed(result);
        Assert.Contains("KeyRetirementGrace", result.FailureMessage);
    }

    [Fact]
    public void Jwt_AlgorithmOtherThanEs256_IsRejected()
    {
        var result = new JwtOptionsValidator().Validate(
            null,
            new JwtOptions
            {
                Issuer = "https://issuer.example",
                Audience = "api",
                Algorithm = "HS256",
            });

        Assert.Failed(result);
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

        Assert.Failed(result);
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

        Assert.Failed(result);
        Assert.Contains("AccessTokenLifetime", result.FailureMessage);
    }

    [Fact]
    public void Cookie_UnsafeNamesAndRootScopedRefresh_AreRejected()
    {
        var result = new AuthCookieOptionsValidator().Validate(
            null,
            new AuthCookieOptions
            {
                AccessCookieName = "auth.access",
                RefreshCookiePath = "/",
            });

        Assert.Failed(result);
    }

    [Fact]
    public void Cors_WildcardOrPathOrigin_IsRejected()
    {
        var validator = new ApiCorsOptionsValidator();

        Assert.Failed(validator.Validate(
            null,
            new ApiCorsOptions { AllowedOrigins = ["*"] }));
        Assert.Failed(validator.Validate(
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

        Assert.Failed(result);
        Assert.Contains("ClientSecret", result.FailureMessage);
    }

    [Fact]
    public void ReverseProxy_ProductionWithoutExplicitTrustBoundary_IsRejected()
    {
        var environment = Substitute.For<IHostEnvironment>();
        environment.EnvironmentName.Returns(Environments.Production);

        var result = new ReverseProxyOptionsValidator(environment).Validate(
            null,
            new ReverseProxyOptions());

        Assert.Failed(result);
        Assert.Contains("Enabled", result.FailureMessage);
    }

    [Fact]
    public void ReverseProxy_InvalidProxyAndNetwork_AreRejected()
    {
        var environment = Substitute.For<IHostEnvironment>();
        environment.EnvironmentName.Returns(Environments.Production);

        var result = new ReverseProxyOptionsValidator(environment).Validate(
            null,
            new ReverseProxyOptions
            {
                Enabled = true,
                KnownProxies = ["not-an-ip"],
                KnownNetworks = ["10.0.0.1/not-a-prefix"],
            });

        Assert.Failed(result);
        Assert.Contains("invalid IP", result.FailureMessage);
        Assert.Contains("invalid CIDR", result.FailureMessage);
    }

    [Fact]
    public void Telemetry_EnabledExporterWithoutEndpoint_IsRejected()
    {
        var result = new TelemetryOptionsValidator().Validate(
            null,
            new TelemetryOptions { OtlpExporterEnabled = true });

        Assert.Failed(result);
        Assert.Contains("OtlpEndpoint", result.FailureMessage);
    }

    [Fact]
    public void CrossFieldDefaults_AreValid()
    {
        Assert.Succeeded(new JwtOptionsValidator().Validate(
            null,
            new JwtOptions
            {
                Issuer = "https://issuer.example",
                Audience = "api",
            }));
        Assert.Succeeded(SessionValidator().Validate(null, new AuthSessionOptions()));
        Assert.Succeeded(new AuthCookieOptionsValidator().Validate(null, new AuthCookieOptions()));
        Assert.Succeeded(new ApiCorsOptionsValidator().Validate(null, new ApiCorsOptions()));
        Assert.Succeeded(new SocialProviderOptionsValidator().Validate(null, new SocialProviderOptions()));
        var development = Substitute.For<IHostEnvironment>();
        development.EnvironmentName.Returns(Environments.Development);
        Assert.Succeeded(new ReverseProxyOptionsValidator(development).Validate(
            null,
            new ReverseProxyOptions()));
        Assert.Succeeded(new TelemetryOptionsValidator().Validate(null, new TelemetryOptions()));
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
