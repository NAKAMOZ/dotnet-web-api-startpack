using Microsoft.AspNetCore.Hosting;

namespace IntegrationTests.Infrastructure;

internal static class WebHostBuilderExtensions
{
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
