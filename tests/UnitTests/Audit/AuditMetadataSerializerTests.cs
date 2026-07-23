using Api.Services.Audit;

namespace UnitTests.Audit;

/// <summary>
/// The audit table's <c>Metadata</c> column is durable, exempt from log rotation and readable
/// over HTTP — the worst place for a credential to land (§15).
/// </summary>
public class AuditMetadataSerializerTests
{
    [Fact]
    public void NullMetadataStaysNull()
    {
        // Not "{}" — an empty object claims there was detail to record.
        Assert.Null(AuditMetadataSerializer.Serialize(null));
    }

    [Fact]
    public void ASecretNamedFieldIsRedacted()
    {
        var json = AuditMetadataSerializer.Serialize(new { RefreshToken = "rt_live_value", Attempt = 3 });

        Assert.NotNull(json);
        Assert.DoesNotContain("rt_live_value", json, StringComparison.Ordinal);
        Assert.Contains("\"attempt\":3", json, StringComparison.Ordinal);
    }

    [Fact]
    public void ANestedSecretIsRedacted()
    {
        // The walk is over the serialized tree, not over a declared type, so nesting and
        // anonymous types are covered by the same pass.
        var json = AuditMetadataSerializer.Serialize(new { Outer = new { ApiKeyHash = "deadbeef" } });

        Assert.NotNull(json);
        Assert.DoesNotContain("deadbeef", json, StringComparison.Ordinal);
    }

    [Fact]
    public void ASecretNamedObjectLosesItsWholeSubtree()
    {
        // A field called `tokens` holding an object is exactly as disqualifying as one
        // holding a string — redacting only scalar leaves would leave the values in place.
        var json = AuditMetadataSerializer.Serialize(new { Tokens = new { Access = "at_value", Refresh = "rt_value" } });

        Assert.NotNull(json);
        Assert.DoesNotContain("at_value", json, StringComparison.Ordinal);
        Assert.DoesNotContain("rt_value", json, StringComparison.Ordinal);
    }

    [Fact]
    public void SecretsInsideArraysAreRedacted()
    {
        var json = AuditMetadataSerializer.Serialize(new
        {
            Sessions = new[] { new { Id = 1, TokenHash = "hash-one" }, new { Id = 2, TokenHash = "hash-two" } },
        });

        Assert.NotNull(json);
        Assert.DoesNotContain("hash-one", json, StringComparison.Ordinal);
        Assert.DoesNotContain("hash-two", json, StringComparison.Ordinal);
        Assert.Contains("\"id\":2", json, StringComparison.Ordinal);
    }

    [Fact]
    public void AnEmailInMetadataIsMasked()
    {
        // The typed columns of the trail store an address in full; free-form metadata gets the
        // logging rule instead.
        var json = AuditMetadataSerializer.Serialize(new { Email = "nevzat@example.com" });

        Assert.NotNull(json);
        Assert.Contains("n***@example.com", json, StringComparison.Ordinal);
        Assert.DoesNotContain("nevzat@example.com", json, StringComparison.Ordinal);
    }

    [Fact]
    public void AFieldNamedLikeAnEmailButHoldingSomethingElseIsRedacted()
    {
        // The masking rule assumes a shape this value does not have. Applying it anyway would
        // reveal an arbitrary prefix of an unknown string.
        var json = AuditMetadataSerializer.Serialize(new { EmailProvider = "smtp-relay-cluster-3" });

        Assert.NotNull(json);
        Assert.DoesNotContain("smtp-relay-cluster-3", json, StringComparison.Ordinal);
    }

    [Fact]
    public void ASingleCharacterLocalPartIsNotRevealed()
    {
        var json = AuditMetadataSerializer.Serialize(new { Email = "n@example.com" });

        Assert.NotNull(json);
        Assert.Contains("****@example.com", json, StringComparison.Ordinal);
    }
}
