using System.ComponentModel.DataAnnotations;

namespace Api.Configuration;

/// <summary>Hard bounds applied before request bodies reach model binding.</summary>
public sealed class RequestSecurityOptions
{
    public const string SectionName = "RequestSecurity";

    /// <summary>
    /// Maximum request-body size. Auth payloads are small; 64 KiB leaves ample room for
    /// WebAuthn JSON while bounding memory, buffering and parser work on anonymous routes.
    /// </summary>
    [Range(1024, 10 * 1024 * 1024)]
    public long MaxRequestBodySizeBytes { get; init; } = 64 * 1024;
}
