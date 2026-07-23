using Api.Middleware;

namespace UnitTests.Middleware;

/// <summary>
/// The inbound-header policy for correlation ids (§14).
/// </summary>
/// <remarks>
/// This value is caller-controlled and ends up in log lines, audit rows and response bodies.
/// The rules are cheap to state and expensive to get wrong, so they are pinned here rather
/// than left to an integration assertion.
/// </remarks>
public class CorrelationIdTests
{
    [Theory]
    [InlineData("b4f0c8de-6f7a-4a4a-9f1e-2f7b1a0d3c9e")]
    [InlineData("0af7651916cd43dd8448eb211c80319c")]
    [InlineData("order-4711.retry_2")]
    public void WellFormedInboundIdsAreAdopted(string inbound) =>
        Assert.True(CorrelationId.IsWellFormed(inbound));

    [Theory]
    [InlineData(null)]
    [InlineData("")]

    // Log forging: a value carrying a newline writes what looks like a second log entry.
    [InlineData("abc\r\ndef")]
    [InlineData("abc\ndef")]

    // JSON-sink framing, and anything that has to be escaped somewhere downstream.
    [InlineData("{\"user\":\"admin\"}")]
    [InlineData("id with spaces")]
    [InlineData("<script>")]
    [InlineData("../../etc/passwd")]
    public void MalformedInboundIdsAreRejected(string? inbound) =>
        Assert.False(CorrelationId.IsWellFormed(inbound));

    [Fact]
    public void OverlongInboundIdsAreRejected()
    {
        // Unbounded, this is a free channel into the log pipeline: the caller pays for one
        // request and the operator pays for storing whatever they sent.
        Assert.True(CorrelationId.IsWellFormed(new string('a', CorrelationId.MaxLength)));
        Assert.False(CorrelationId.IsWellFormed(new string('a', CorrelationId.MaxLength + 1)));
    }

    [Fact]
    public void GeneratedIdsAreWellFormedAndDistinct()
    {
        // Self-consistency: a generated id must pass the same gate an inbound one does, or a
        // downstream sink that re-validates would reject our own value.
        var first = CorrelationId.New();
        var second = CorrelationId.New();

        Assert.True(CorrelationId.IsWellFormed(first));
        Assert.NotEqual(first, second);
    }
}
