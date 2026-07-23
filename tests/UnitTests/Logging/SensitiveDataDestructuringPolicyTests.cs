using Api.DTOs.Auth;
using Api.Logging;
using Serilog;

namespace UnitTests.Logging;

/// <summary>
/// The mechanical half of ADR-0010's never-logged rule (§15): what happens when a whole object
/// of ours is destructured into a log event.
/// </summary>
public class SensitiveDataDestructuringPolicyTests
{
    [Fact]
    public void ASecretNamedPropertyNeverReachesTheSink()
    {
        var request = new LoginRequest { Email = "nevzat@example.com", Password = "correct horse battery staple" };

        var rendered = Capture("{@Request}", request);

        Assert.DoesNotContain("correct horse battery staple", rendered, StringComparison.Ordinal);
        Assert.Contains(SensitiveFieldNames.RedactedValue, rendered, StringComparison.Ordinal);
    }

    [Fact]
    public void AnEmailIsMaskedRatherThanRedacted()
    {
        // Masked, not removed: "a hundred failed logins, all at one domain" has to stay
        // readable, and an operator needs to recognise the account in the line.
        var request = new LoginRequest { Email = "nevzat@example.com", Password = "irrelevant" };

        var rendered = Capture("{@Request}", request);

        Assert.Contains("n***@example.com", rendered, StringComparison.Ordinal);
        Assert.DoesNotContain("nevzat@example.com", rendered, StringComparison.Ordinal);
    }

    [Fact]
    public void AScalarPassedUnderAnInnocentNameIsNotProtected()
    {
        // Not a defect — the boundary of what a destructuring policy can do, asserted so the
        // next reader does not mistake this policy for total coverage. A raw string logged
        // under a name of the author's choosing is indistinguishable from any other string.
        // §22's log-capture test and code review are the other half.
        var rendered = Capture("{Value}", "a-real-token-value");

        Assert.Contains("a-real-token-value", rendered, StringComparison.Ordinal);
    }

    [Fact]
    public void FrameworkTypesKeepTheirDefaultHandling()
    {
        // The policy claims only types from the API assembly. A Uri destructured here would
        // otherwise be walked property by property for no benefit.
        var rendered = Capture("{@Value}", new Uri("https://example.com/path"));

        Assert.Contains("example.com", rendered, StringComparison.Ordinal);
    }

    private static string Capture(string messageTemplate, object value)
    {
        var sink = new CollectingSink();

        using var logger = new LoggerConfiguration()
            .Destructure.With(new SensitiveDataDestructuringPolicy())
            .WriteTo.Sink(sink)
            .CreateLogger();

        logger.Information(messageTemplate, value);

        var logEvent = Assert.Single(sink.Events);

        using var writer = new StringWriter();
        logEvent.RenderMessage(writer);

        return writer.ToString();
    }
}
