using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Api.DTOs.Auth;
using Api.DTOs.PasswordReset;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace IntegrationTests;

public class RateLimitingTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public RateLimitingTests(WebApplicationFactory<Program> factory) => _factory = factory;

    [Fact]
    public async Task AuthStrict_Exhausted_ReturnsProblemDetailsAndRetryAfter()
    {
        var client = CreateClient(new Dictionary<string, string?>
        {
            ["RateLimiting:AuthStrictPermitLimit"] = "2",
            ["RateLimiting:AuthStrictWindow"] = "00:10:00",
        });

        var request = new LoginRequest
        {
            Email = "login@example.com",
            Password = "wrong password",
        };

        Assert.Equal(
            HttpStatusCode.NotImplemented,
            (await client.PostAsJsonAsync(
                "/api/v1/auth/login",
                request,
                TestContext.Current.CancellationToken)).StatusCode);

        Assert.Equal(
            HttpStatusCode.NotImplemented,
            (await client.PostAsJsonAsync(
                "/api/v1/auth/login",
                request,
                TestContext.Current.CancellationToken)).StatusCode);

        var rejected = await client.PostAsJsonAsync(
            "/api/v1/auth/login",
            request,
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.TooManyRequests, rejected.StatusCode);
        Assert.True(rejected.Headers.TryGetValues("Retry-After", out var retryAfter));
        Assert.True(int.Parse(retryAfter.Single()) > 0);
        Assert.Equal("application/problem+json", rejected.Content.Headers.ContentType?.MediaType);

        var problem = await rejected.Content.ReadFromJsonAsync<JsonElement>(
            TestContext.Current.CancellationToken);

        Assert.Equal("rate_limited", problem.GetProperty("errorCode").GetString());
        Assert.True(problem.TryGetProperty("correlationId", out _));
        Assert.True(problem.TryGetProperty("traceId", out _));

        var otherIp = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/login")
        {
            Content = JsonContent.Create(request),
        };
        otherIp.Headers.Add(TestRemoteIpStartupFilter.HeaderName, "203.0.113.20");

        Assert.Equal(
            HttpStatusCode.NotImplemented,
            (await client.SendAsync(
                otherIp,
                TestContext.Current.CancellationToken)).StatusCode);
    }

    [Fact]
    public async Task EmailSending_TargetAccountsHaveIndependentAllowances()
    {
        var client = CreateClient(new Dictionary<string, string?>
        {
            ["RateLimiting:EmailSendingIpPermitLimit"] = "10",
            ["RateLimiting:EmailSendingAccountPermitLimit"] = "2",
            ["RateLimiting:EmailSendingAccountWindow"] = "01:00:00",
        });

        var firstTarget = new PasswordResetRequest { Email = "victim@example.com" };

        Assert.Equal(
            HttpStatusCode.NotImplemented,
            (await PostPasswordResetAsync(client, firstTarget, "198.51.100.1")).StatusCode);
        Assert.Equal(
            HttpStatusCode.NotImplemented,
            (await PostPasswordResetAsync(client, firstTarget, "198.51.100.2")).StatusCode);

        // One target, three IPs: rotating the source address does not rotate the victim's
        // account partition.
        var rejected = await PostPasswordResetAsync(client, firstTarget, "198.51.100.3");

        Assert.Equal(HttpStatusCode.TooManyRequests, rejected.StatusCode);
        Assert.True(rejected.Headers.Contains("Retry-After"));

        // Same IP, different account: the victim's quota does not suppress unrelated users.
        var independent = await PostPasswordResetAsync(
            client,
            new PasswordResetRequest { Email = "other@example.com" },
            "198.51.100.3");

        Assert.Equal(HttpStatusCode.NotImplemented, independent.StatusCode);
    }

    [Fact]
    public async Task Registration_UsesAnIndependentFixedWindowPolicy()
    {
        var client = CreateClient(new Dictionary<string, string?>
        {
            ["RateLimiting:RegistrationPermitLimit"] = "1",
            ["RateLimiting:RegistrationWindow"] = "01:00:00",
        });

        var request = new RegisterRequest
        {
            Email = "new@example.com",
            Password = "V4lid!River-Stone-Cobalt-47",
            DisplayName = "New User",
        };

        Assert.Equal(
            HttpStatusCode.NotImplemented,
            (await client.PostAsJsonAsync(
                "/api/v1/auth/register",
                request,
                TestContext.Current.CancellationToken)).StatusCode);

        Assert.Equal(
            HttpStatusCode.TooManyRequests,
            (await client.PostAsJsonAsync(
                "/api/v1/auth/register",
                request,
                TestContext.Current.CancellationToken)).StatusCode);
    }

    [Fact]
    public async Task EmailSending_ClientIpCapAppliesAcrossDifferentTargets()
    {
        var client = CreateClient(new Dictionary<string, string?>
        {
            ["RateLimiting:EmailSendingIpPermitLimit"] = "1",
            ["RateLimiting:EmailSendingAccountPermitLimit"] = "10",
        });

        Assert.Equal(
            HttpStatusCode.NotImplemented,
            (await PostPasswordResetAsync(
                client,
                new PasswordResetRequest { Email = "first@example.com" },
                "203.0.113.10")).StatusCode);

        Assert.Equal(
            HttpStatusCode.TooManyRequests,
            (await PostPasswordResetAsync(
                client,
                new PasswordResetRequest { Email = "second@example.com" },
                "203.0.113.10")).StatusCode);
    }

    [Fact]
    public async Task GeneralPolicy_CoversEndpointsWithoutANamedPolicy()
    {
        var client = CreateClient(new Dictionary<string, string?>
        {
            ["RateLimiting:GeneralPermitLimit"] = "1",
            ["RateLimiting:GeneralWindow"] = "00:10:00",
            ["RateLimiting:AuthStrictPermitLimit"] = "100",
        }, overrideGeneralLimit: false);

        var login = new LoginRequest
        {
            Email = "login@example.com",
            Password = "wrong password",
        };

        Assert.Equal(
            HttpStatusCode.NotImplemented,
            (await client.PostAsJsonAsync(
                "/api/v1/auth/login",
                login,
                TestContext.Current.CancellationToken)).StatusCode);

        Assert.Equal(
            HttpStatusCode.TooManyRequests,
            (await client.GetAsync(
                "/api/v1/auth/csrf",
                TestContext.Current.CancellationToken)).StatusCode);
    }

    private HttpClient CreateClient(
        IReadOnlyDictionary<string, string?> overrides,
        bool overrideGeneralLimit = true)
    {
        var factory = _factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Production");
            builder.UseTrustedTestProxy();
            builder.UseSetting(
                "ConnectionStrings:Postgres",
                "Host=localhost;Database=rate-limiting-tests");

            var settings = new Dictionary<string, string?>(overrides);

            if (overrideGeneralLimit)
            {
                settings["RateLimiting:GeneralPermitLimit"] = "1000";
            }

            builder.ConfigureAppConfiguration(configuration =>
                configuration.AddInMemoryCollection(settings));
            builder.ConfigureServices(services =>
                services.AddSingleton<IStartupFilter, TestRemoteIpStartupFilter>());
        });

        return factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
    }

    private static Task<HttpResponseMessage> PostPasswordResetAsync(
        HttpClient client,
        PasswordResetRequest body,
        string remoteIp)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/password-reset/request")
        {
            Content = JsonContent.Create(body),
        };

        request.Headers.Add(TestRemoteIpStartupFilter.HeaderName, remoteIp);

        return client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    private sealed class TestRemoteIpStartupFilter : IStartupFilter
    {
        public const string HeaderName = "X-Test-Remote-Ip";

        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) =>
            application =>
            {
                application.Use(async (context, following) =>
                {
                    if (context.Request.Headers.TryGetValue(HeaderName, out var value)
                        && IPAddress.TryParse(value.ToString(), out var address))
                    {
                        context.Connection.RemoteIpAddress = address;
                    }

                    await following();
                });

                next(application);
            };
    }
}
