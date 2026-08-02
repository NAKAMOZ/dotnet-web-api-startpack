using Api.Configuration;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace IntegrationTests.Infrastructure;

internal static class WebHostBuilderExtensions
{
    /// <summary>
    /// The whole preamble a non-Development TestServer host needs before it will boot.
    /// </summary>
    /// <remarks>
    /// Every production-only startup rule belongs here rather than at the call sites. The
    /// proxy contract was the first; adding the second to seven test classes by hand is how
    /// the eighth gets missed, and the symptom is an options-validation stack trace out of
    /// <c>CreateClient()</c> rather than anything naming the rule.
    /// </remarks>
    /// <param name="environment">The host environment name — anything but Development.</param>
    /// <param name="databaseName">
    /// Names the throwaway connection string. §7 fails the boot when none is configured and
    /// no committed file carries one; nothing in these tests ever connects through it.
    /// </param>
    public static IWebHostBuilder UseProductionLikeHost(
        this IWebHostBuilder builder,
        string environment,
        string databaseName)
    {
        builder.UseEnvironment(environment);
        builder.UseTrustedTestProxy();
        builder.UseSetting("ConnectionStrings:Postgres", $"Host=localhost;Database={databaseName}");
        builder.ConfigureAppConfiguration(configuration =>
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Azure:DataProtectionKeyIdentifier"] =
                    "https://unit-tests.vault.azure.net/keys/data-protection",
                ["Jwt:Issuer"] = "https://auth.test.example",
                ["WebAuthn:ServerDomain"] = "auth.test.example",
                ["WebAuthn:Origins:0"] = "https://auth.test.example",
                ["WebAuthn:Origins:1"] = "https://login.auth.test.example",
                ["Email:Host"] = "smtp.test.example",
                ["Email:UseTls"] = "true",
            }));
        // Array binding combines values from multiple providers. Replace, rather than append
        // to, appsettings' localhost origins for production-like TestServer hosts.
        builder.ConfigureServices(services =>
            services.PostConfigure<WebAuthnOptions>(options =>
                options.Origins =
                [
                    "https://auth.test.example",
                    "https://login.auth.test.example",
                ]));
        return builder;
    }

    /// <summary>
    /// Satisfies the production-like fail-fast proxy contract for in-memory TestServer hosts.
    /// TestServer never supplies forwarded headers unless a test does so explicitly.
    /// </summary>
    public static IWebHostBuilder UseTrustedTestProxy(this IWebHostBuilder builder)
    {
        builder.UseSetting("ReverseProxy:Enabled", "true");
        builder.UseSetting("ReverseProxy:KnownProxies:0", "127.0.0.1");
        builder.UseSetting("ReverseProxy:KnownProxies:1", "::1");
        return builder;
    }
}
