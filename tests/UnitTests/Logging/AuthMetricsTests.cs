using System.Collections.Concurrent;
using System.Diagnostics.Metrics;
using Api.Logging;

namespace UnitTests.Logging;

public sealed class AuthMetricsTests
{
    [Fact]
    public void CatalogedAuthenticationMetrics_AreEmitted()
    {
        var observed = new ConcurrentBag<string>();
        using var listener = new MeterListener
        {
            InstrumentPublished = (instrument, meterListener) =>
            {
                if (instrument.Meter.Name == AuthMetrics.MeterName)
                {
                    meterListener.EnableMeasurementEvents(instrument);
                }
            },
        };

        listener.SetMeasurementEventCallback<long>(
            (instrument, _, _, _) => observed.Add(instrument.Name));
        listener.SetMeasurementEventCallback<double>(
            (instrument, _, _, _) => observed.Add(instrument.Name));
        listener.Start();

        var metrics = new AuthMetrics();
        metrics.RecordLogin("success");
        metrics.RecordRefresh("success");
        metrics.RecordReuseDetection();
        metrics.RecordLockout();
        metrics.RecordMfaChallenge("success");
        metrics.RecordPasswordHashDuration(TimeSpan.FromMilliseconds(123), "verify");
        metrics.SetActiveSessions(7);
        listener.RecordObservableInstruments();

        Assert.Contains("auth.logins", observed);
        Assert.Contains("auth.refreshes", observed);
        Assert.Contains("auth.reuse_detections", observed);
        Assert.Contains("auth.lockouts", observed);
        Assert.Contains("auth.mfa_challenges", observed);
        Assert.Contains("auth.password_hash_duration", observed);
        Assert.Contains("auth.active_sessions", observed);
    }
}
