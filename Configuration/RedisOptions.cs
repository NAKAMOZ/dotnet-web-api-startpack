using System.ComponentModel.DataAnnotations;

namespace Api.Configuration;

/// <summary>
/// Shared Redis connection used by HybridCache and distributed rate-limit counters.
/// </summary>
public sealed class RedisOptions
{
    public const string SectionName = "Redis";

    /// <summary>Whether Redis-backed distributed runtime state is enabled.</summary>
    public bool Enabled { get; init; }

    /// <summary>
    /// StackExchange.Redis endpoint. Azure Managed Redis uses
    /// <c>{cache}.{region}.redis.azure.net:10000</c>.
    /// </summary>
    public string? Endpoint { get; init; }

    /// <summary>
    /// Authenticate with Microsoft Entra ID and the managed identity configured in Azure
    /// options. Access-key authentication remains available for non-Azure test stacks only.
    /// </summary>
    public bool UseAzureIdentity { get; init; }

    /// <summary>Namespace prefix preventing collisions when one Redis database is shared.</summary>
    [Required]
    [RegularExpression("^[A-Za-z0-9:._-]{1,64}$")]
    public string InstanceName { get; init; } = "startpack:";

    /// <summary>Connection establishment timeout in milliseconds.</summary>
    [Range(1_000, 60_000)]
    public int ConnectTimeoutMilliseconds { get; init; } = 10_000;
}
