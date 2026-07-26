using Api.Mappings;
using Api.Models;
using Api.Models.Enums;

namespace UnitTests.Mappings;

public class AuditMappingExtensionsTests
{
    [Fact]
    public void ToResponse_MapsEveryStoredFieldAndKeepsMetadataAsJson()
    {
        var entry = new AuditLogEntry
        {
            Id = Guid.Parse("01900000-0000-7000-8000-000000000020"),
            UserId = Guid.Parse("01900000-0000-7000-8000-000000000021"),
            EventType = AuditEventType.LoginFailed,
            IpAddress = "192.0.2.10",
            UserAgent = "test-agent",
            CorrelationId = "test-correlation",
            Metadata = """{"reason":"wrong_password","attempt":2}""",
            OccurredAt = new DateTimeOffset(2026, 7, 26, 16, 0, 0, TimeSpan.Zero),
        };

        var response = entry.ToResponse();

        Assert.Equal(entry.Id, response.Id);
        Assert.Equal(entry.UserId, response.UserId);
        Assert.Equal(entry.EventType, response.EventType);
        Assert.Equal(entry.IpAddress, response.IpAddress);
        Assert.Equal(entry.UserAgent, response.UserAgent);
        Assert.Equal(entry.CorrelationId, response.CorrelationId);
        Assert.Equal(entry.OccurredAt, response.OccurredAt);
        Assert.Equal("wrong_password", response.Metadata?.GetProperty("reason").GetString());
        Assert.Equal(2, response.Metadata?.GetProperty("attempt").GetInt32());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("{not-json}")]
    public void ToResponse_AbsentOrCorruptMetadata_DoesNotFailTheAuditPage(string? metadata)
    {
        var response = new AuditLogEntry
        {
            EventType = AuditEventType.LoginSucceeded,
            Metadata = metadata,
        }.ToResponse();

        Assert.Null(response.Metadata);
    }
}
