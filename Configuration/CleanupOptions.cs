using System.ComponentModel.DataAnnotations;

namespace Api.Configuration;

/// <summary>Retention and cadence for the maintenance workers introduced with §12.</summary>
public sealed class CleanupOptions
{
    public const string SectionName = "Cleanup";

    [Range(typeof(TimeSpan), "00:01:00", "7.00:00:00")]
    public TimeSpan Interval { get; init; } = TimeSpan.FromHours(1);

    [Range(typeof(TimeSpan), "1.00:00:00", "730.00:00:00")]
    public TimeSpan AuditRetention { get; init; } = TimeSpan.FromDays(90);

    [Range(1, 100_000)]
    public int BatchSize { get; init; } = 1_000;
}
