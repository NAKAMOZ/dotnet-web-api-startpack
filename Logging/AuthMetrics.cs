using System.Diagnostics.Metrics;

namespace Api.Logging;

/// <summary>
/// Low-cardinality authentication metrics. No user, email, session, token or IP is ever a tag.
/// </summary>
public sealed class AuthMetrics
{
    public const string MeterName = "Api.Authentication";

    private static readonly Meter Meter = new(MeterName);
    private static readonly Counter<long> Logins = Meter.CreateCounter<long>(
        "auth.logins",
        unit: "{attempt}");
    private static readonly Counter<long> Refreshes = Meter.CreateCounter<long>(
        "auth.refreshes",
        unit: "{attempt}");
    private static readonly Counter<long> ReuseDetections = Meter.CreateCounter<long>(
        "auth.reuse_detections",
        unit: "{detection}");
    private static readonly Counter<long> Lockouts = Meter.CreateCounter<long>(
        "auth.lockouts",
        unit: "{lockout}");
    private static readonly Counter<long> MfaChallenges = Meter.CreateCounter<long>(
        "auth.mfa_challenges",
        unit: "{challenge}");
    private static readonly Histogram<double> PasswordHashDuration = Meter.CreateHistogram<double>(
        "auth.password_hash_duration",
        unit: "ms");
    private static readonly ObservableGauge<long> ActiveSessions = Meter.CreateObservableGauge(
        "auth.active_sessions",
        ObserveActiveSessions,
        unit: "{session}");

    private static long _activeSessionCount;
    private static int _hasActiveSessionSample;

    public bool HasActiveSessionSample => Volatile.Read(ref _hasActiveSessionSample) == 1;

    public void RecordLogin(string result) =>
        Logins.Add(1, new KeyValuePair<string, object?>("result", result));

    public void RecordRefresh(string result) =>
        Refreshes.Add(1, new KeyValuePair<string, object?>("result", result));

    public void RecordReuseDetection() => ReuseDetections.Add(1);

    public void RecordLockout() => Lockouts.Add(1);

    public void RecordMfaChallenge(string result) =>
        MfaChallenges.Add(1, new KeyValuePair<string, object?>("result", result));

    public void RecordPasswordHashDuration(TimeSpan duration, string operation) =>
        PasswordHashDuration.Record(
            duration.TotalMilliseconds,
            new KeyValuePair<string, object?>("operation", operation));

    public void SetActiveSessions(long count)
    {
        Interlocked.Exchange(ref _activeSessionCount, Math.Max(0, count));
        Volatile.Write(ref _hasActiveSessionSample, 1);
    }

    private static IEnumerable<Measurement<long>> ObserveActiveSessions()
    {
        if (Volatile.Read(ref _hasActiveSessionSample) == 1)
        {
            yield return new Measurement<long>(Interlocked.Read(ref _activeSessionCount));
        }
    }
}
