using Microsoft.AspNetCore.Hosting;

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
