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

        OptionsAssert.Failed(result);
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
    public void CrossFieldDefaults_AreValid()
    {
        OptionsAssert.Succeeded(new JwtOptionsValidator().Validate(
            null,
            new JwtOptions
            {
                Issuer = "https://issuer.example",
                Audience = "api",
            }));
        OptionsAssert.Succeeded(SessionValidator().Validate(null, new AuthSessionOptions()));
        var development = Environment(Environments.Development);
        OptionsAssert.Succeeded(new AuthCookieOptionsValidator(development).Validate(
            null,
            new AuthCookieOptions()));
        OptionsAssert.Succeeded(new ApiCorsOptionsValidator().Validate(null, new ApiCorsOptions()));
        OptionsAssert.Succeeded(new SocialProviderOptionsValidator().Validate(null, new SocialProviderOptions()));
        OptionsAssert.Succeeded(new ReverseProxyOptionsValidator(development).Validate(
            null,
            new ReverseProxyOptions()));
        OptionsAssert.Succeeded(new TelemetryOptionsValidator().Validate(null, new TelemetryOptions()));
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
