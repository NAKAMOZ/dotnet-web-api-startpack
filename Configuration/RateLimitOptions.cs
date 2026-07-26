using System.ComponentModel.DataAnnotations;

namespace Api.Configuration;

/// <summary>
/// Configurable abuse-prevention limits (§17). All queues are deliberately disabled:
/// authentication work should be rejected when capacity is exhausted, not accumulated in
/// memory for later execution.
/// </summary>
public sealed class RateLimitOptions
{
    public const string SectionName = "RateLimiting";

    [Range(1, 10_000)]
    public int AuthStrictPermitLimit { get; init; } = 10;

    [Range(typeof(TimeSpan), "00:00:01", "1.00:00:00")]
    public TimeSpan AuthStrictWindow { get; init; } = TimeSpan.FromMinutes(1);

    [Range(1, 10_000)]
    public int EmailSendingIpPermitLimit { get; init; } = 5;

    [Range(typeof(TimeSpan), "00:01:00", "7.00:00:00")]
    public TimeSpan EmailSendingIpWindow { get; init; } = TimeSpan.FromHours(1);

    [Range(1, 10_000)]
    public int EmailSendingAccountPermitLimit { get; init; } = 3;

    [Range(typeof(TimeSpan), "00:01:00", "7.00:00:00")]
    public TimeSpan EmailSendingAccountWindow { get; init; } = TimeSpan.FromHours(1);

    [Range(1, 10_000)]
    public int RegistrationPermitLimit { get; init; } = 5;

    [Range(typeof(TimeSpan), "00:01:00", "7.00:00:00")]
    public TimeSpan RegistrationWindow { get; init; } = TimeSpan.FromHours(1);

    [Range(1, 1_000_000)]
    public int GeneralPermitLimit { get; init; } = 100;

    [Range(typeof(TimeSpan), "00:00:01", "1.00:00:00")]
    public TimeSpan GeneralWindow { get; init; } = TimeSpan.FromMinutes(1);

    [Range(1, 60)]
    public int GeneralSegmentsPerWindow { get; init; } = 6;
}
