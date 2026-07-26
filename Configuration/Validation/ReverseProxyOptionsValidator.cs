using System.Net;
using Microsoft.Extensions.Options;

namespace Api.Configuration.Validation;

/// <summary>Validates that forwarded headers can only cross an explicit proxy boundary.</summary>
public sealed class ReverseProxyOptionsValidator(IHostEnvironment environment)
    : IValidateOptions<ReverseProxyOptions>
{
    public ValidateOptionsResult Validate(string? name, ReverseProxyOptions options)
    {
        var failures = new List<string>();
        var productionLike = !environment.IsDevelopment() && !environment.IsEnvironment("Testing");

        if (productionLike && !options.Enabled)
        {
            failures.Add("ReverseProxy:Enabled must be true outside Development and Testing.");
        }

        if (!options.Enabled)
        {
            return failures.Count == 0
                ? ValidateOptionsResult.Success
                : ValidateOptionsResult.Fail(failures);
        }

        if (options.KnownProxies.Length == 0 && options.KnownNetworks.Length == 0)
        {
            failures.Add(
                "At least one ReverseProxy:KnownProxies or ReverseProxy:KnownNetworks entry is required.");
        }

        foreach (var proxy in options.KnownProxies)
        {
            if (!IPAddress.TryParse(proxy, out _))
            {
                failures.Add($"ReverseProxy:KnownProxies contains invalid IP address '{proxy}'.");
            }
        }

        foreach (var network in options.KnownNetworks)
        {
            if (!IPNetwork.TryParse(network, out _))
            {
                failures.Add($"ReverseProxy:KnownNetworks contains invalid CIDR '{network}'.");
            }
        }

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }
}
