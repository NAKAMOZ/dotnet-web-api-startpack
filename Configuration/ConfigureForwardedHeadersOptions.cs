using System.Net;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Options;

namespace Api.Configuration;

/// <summary>Projects validated operator settings into ASP.NET Core's forwarded-header options.</summary>
public sealed class ConfigureForwardedHeadersOptions(IOptions<ReverseProxyOptions> configured)
    : IConfigureOptions<ForwardedHeadersOptions>
{
    public void Configure(ForwardedHeadersOptions options)
    {
        var settings = configured.Value;

        options.ForwardedHeaders =
            ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
        options.ForwardLimit = settings.ForwardLimit;
        options.RequireHeaderSymmetry = true;

        // The framework defaults to trusting loopback. Defaults are inappropriate for a
        // declared production topology: only the operator's exact list is authoritative.
        options.KnownProxies.Clear();
        options.KnownIPNetworks.Clear();

        foreach (var proxy in settings.KnownProxies)
        {
            options.KnownProxies.Add(IPAddress.Parse(proxy));
        }

        foreach (var network in settings.KnownNetworks)
        {
            options.KnownIPNetworks.Add(System.Net.IPNetwork.Parse(network));
        }
    }
}
