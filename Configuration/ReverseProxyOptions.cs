using System.ComponentModel.DataAnnotations;

namespace Api.Configuration;

/// <summary>
/// Trust boundary for a TLS-terminating reverse proxy (§27).
/// </summary>
/// <remarks>
/// Forwarded headers are attacker-controlled unless the immediate sender is known. The
/// allowlists therefore describe proxies, not clients, and an empty list never means
/// "trust everyone".
/// </remarks>
public sealed class ReverseProxyOptions
{
    public const string SectionName = "ReverseProxy";

    /// <summary>
    /// Enables processing of <c>X-Forwarded-For</c> and <c>X-Forwarded-Proto</c>.
    /// Required outside Development and Testing.
    /// </summary>
    public bool Enabled { get; init; }

    /// <summary>Maximum number of proxy hops consumed from the right side of each header.</summary>
    [Range(1, 5)]
    public int ForwardLimit { get; init; } = 1;

    /// <summary>Exact IP addresses of trusted reverse proxies.</summary>
    public string[] KnownProxies { get; init; } = [];

    /// <summary>Trusted proxy networks in CIDR notation.</summary>
    public string[] KnownNetworks { get; init; } = [];
}
