using Api.Data;
using Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace UnitTests.Data;

/// <summary>
/// Asserts the shape of the built EF model (§7). No database is involved — EF builds the
/// model from the configuration classes without opening a connection, so these run in
/// milliseconds and guard the mappings whose absence is silent.
/// </summary>
/// <remarks>
/// The point is the *silent* failures. A dropped <c>citext</c> column type still stores
/// emails; a dropped unique index still accepts the first registration; a cascade quietly
/// downgraded to <c>Restrict</c> only fails when someone deletes an account. §21 exercises
/// the same model against real PostgreSQL; these tests catch the regressions before a
/// container is even started.
/// </remarks>
public class AppDbContextModelTests
{
    private static IModel BuildModel()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=localhost;Database=model-only")
            .Options;

        using var context = new AppDbContext(options);
        return context.Model;
    }

    /// <summary>
    /// Finds the single-column index over <paramref name="propertyName"/>. Filtering on the
    /// column count first matters: several of these tables also carry composite indexes, and
    /// a predicate that assumes one column throws on those instead of skipping them.
    /// </summary>
    private static IIndex SingleColumnIndex(IEntityType entity, string propertyName) =>
        entity.GetIndexes().Single(index =>
            index.Properties.Count == 1 && index.Properties[0].Name == propertyName);

    [Fact]
    public void EmailIsCaseInsensitiveAndUnique()
    {
        var user = BuildModel().FindEntityType(typeof(User))!;

        Assert.Equal("citext", user.FindProperty(nameof(User.Email))!.GetColumnType());

        Assert.True(SingleColumnIndex(user, nameof(User.Email)).IsUnique);
    }

    [Fact]
    public void RefreshTokenHashIsUnique()
    {
        var refreshToken = BuildModel().FindEntityType(typeof(RefreshToken))!;

        Assert.True(SingleColumnIndex(refreshToken, nameof(RefreshToken.TokenHash)).IsUnique);
    }

    [Theory]
    [InlineData(typeof(RefreshToken), nameof(RefreshToken.ExpiresAt), "\"UsedAt\" IS NULL")]
    [InlineData(typeof(Session), nameof(Session.AbsoluteExpiresAt), "\"RevokedAt\" IS NULL")]
    [InlineData(typeof(VerificationToken), nameof(VerificationToken.ExpiresAt), "\"ConsumedAt\" IS NULL")]
    public void CleanupIndexesArePartial(Type entityType, string propertyName, string expectedFilter)
    {
        var entity = BuildModel().FindEntityType(entityType)!;

        Assert.Equal(expectedFilter, SingleColumnIndex(entity, propertyName).GetFilter());
    }

    [Fact]
    public void OnlyOneSigningKeyMayBeActive()
    {
        var signingKey = BuildModel().FindEntityType(typeof(SigningKey))!;

        var statusIndex = SingleColumnIndex(signingKey, nameof(SigningKey.Status));

        Assert.True(statusIndex.IsUnique);
        Assert.Equal("\"Status\" = 'Active'", statusIndex.GetFilter());
    }

    [Fact]
    public void DeletingAUserDestroysItsCredentialsButNotItsAuditTrail()
    {
        var model = BuildModel();

        var credentialForeignKeys = new[]
        {
            typeof(Session), typeof(Account), typeof(VerificationToken), typeof(TotpCredential),
            typeof(RecoveryCode), typeof(PasskeyCredential), typeof(ApiKey), typeof(UserRole),
        };

        foreach (var entityType in credentialForeignKeys)
        {
            var foreignKey = model.FindEntityType(entityType)!
                .GetForeignKeys()
                .Single(key => key.PrincipalEntityType.ClrType == typeof(User));

            Assert.Equal(DeleteBehavior.Cascade, foreignKey.DeleteBehavior);
        }

        // The deliberate exception: an audit row outlives the account it describes.
        var auditForeignKey = model.FindEntityType(typeof(AuditLogEntry))!.GetForeignKeys().Single();

        Assert.Equal(DeleteBehavior.SetNull, auditForeignKey.DeleteBehavior);
    }

    [Fact]
    public void EveryTimestampIsTimestamptz()
    {
        var timestamps = BuildModel().GetEntityTypes()
            .SelectMany(entity => entity.GetProperties())
            .Where(property => property.ClrType == typeof(DateTimeOffset)
                               || property.ClrType == typeof(DateTimeOffset?));

        Assert.All(timestamps, property => Assert.Equal("timestamptz", property.GetColumnType()));
    }

    [Fact]
    public void EveryEnumIsStoredAsAString()
    {
        var enums = BuildModel().GetEntityTypes()
            .SelectMany(entity => entity.GetProperties())
            .Where(property => (Nullable.GetUnderlyingType(property.ClrType) ?? property.ClrType).IsEnum);

        Assert.NotEmpty(enums);

        // An ordinal column silently re-points existing rows when an enum member is
        // inserted in the middle — and makes the audit table unreadable in the process.
        Assert.All(enums, property => Assert.Equal(typeof(string), property.GetProviderClrType()));
    }

    [Fact]
    public void AuditMetadataIsJsonb()
    {
        var metadata = BuildModel()
            .FindEntityType(typeof(AuditLogEntry))!
            .FindProperty(nameof(AuditLogEntry.Metadata))!;

        Assert.Equal("jsonb", metadata.GetColumnType());
    }

    [Fact]
    public void MutableCollectionsHaveValueComparers()
    {
        var model = BuildModel();

        // Without a comparer EF compares these by reference, so an in-place mutation —
        // key.Scopes.Add(...) — produces no UPDATE and no error either.
        var collections = new (Type Entity, string Property)[]
        {
            (typeof(Session), nameof(Session.AuthenticationMethods)),
            (typeof(ApiKey), nameof(ApiKey.Scopes)),
            (typeof(PasskeyCredential), nameof(PasskeyCredential.Transports)),
        };

        foreach (var (entityType, propertyName) in collections)
        {
            var property = model.FindEntityType(entityType)!.FindProperty(propertyName)!;

            Assert.NotNull(property.GetValueConverter());
            Assert.NotNull(property.GetValueComparer());
        }
    }
}
