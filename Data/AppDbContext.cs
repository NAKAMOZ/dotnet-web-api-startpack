using Api.Models;
using Api.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace Api.Data;

/// <summary>
/// The single unit of work over the auth schema (ADR-0008).
/// </summary>
/// <remarks>
/// <b>There is no repository layer, deliberately.</b> <see cref="DbContext"/> already is a
/// unit of work and <see cref="DbSet{TEntity}"/> already is a repository; wrapping them
/// would add a hop that enables nothing. Integration tests run against real PostgreSQL
/// (§21), so no mocking need exists for a repository to serve.
/// <para>
/// Mapping lives in <c>Data/Configurations/</c> — one
/// <see cref="IEntityTypeConfiguration{TEntity}"/> per entity. Entities stay POCOs with no
/// EF attributes, so <c>Models/</c> is readable as a domain model rather than a schema.
/// </para>
/// </remarks>
public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();

    public DbSet<Session> Sessions => Set<Session>();

    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    public DbSet<Account> Accounts => Set<Account>();

    public DbSet<VerificationToken> VerificationTokens => Set<VerificationToken>();

    public DbSet<TotpCredential> TotpCredentials => Set<TotpCredential>();

    public DbSet<RecoveryCode> RecoveryCodes => Set<RecoveryCode>();

    public DbSet<PasskeyCredential> PasskeyCredentials => Set<PasskeyCredential>();

    public DbSet<ApiKey> ApiKeys => Set<ApiKey>();

    public DbSet<Role> Roles => Set<Role>();

    public DbSet<UserRole> UserRoles => Set<UserRole>();

    public DbSet<AuditLogEntry> AuditLogEntries => Set<AuditLogEntry>();

    public DbSet<SigningKey> SigningKeys => Set<SigningKey>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // citext backs User.Email. Declaring the extension here means the initial migration
        // (§8) emits CREATE EXTENSION, so a fresh database — including the Testcontainers
        // instance the integration tests spin up — matches production without a manual step.
        modelBuilder.HasPostgresExtension("citext");

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        base.OnModelCreating(modelBuilder);
    }

    /// <summary>
    /// Type-level mapping that would otherwise be repeated in all thirteen configuration
    /// classes. Per-entity concerns — keys, indexes, relationships, lengths — stay in
    /// <c>Data/Configurations/</c>.
    /// </summary>
    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // Every timestamp is timestamptz. Postgres stores it as UTC and returns it as UTC,
        // which is what makes "always UTC" a property of the database rather than a rule
        // every query has to remember.
        configurationBuilder.Properties<DateTimeOffset>().HaveColumnType("timestamptz");

        // Enums persist as strings, not ordinals. An ordinal column silently re-points every
        // existing row when a member is inserted in the middle of an enum — and it makes the
        // audit table unreadable without a lookup the reviewer does not have.
        configurationBuilder.Properties<VerificationTokenType>().HaveConversion<string>().HaveMaxLength(48);
        configurationBuilder.Properties<SessionRevocationReason>().HaveConversion<string>().HaveMaxLength(48);
        configurationBuilder.Properties<SigningKeyStatus>().HaveConversion<string>().HaveMaxLength(16);
        configurationBuilder.Properties<AuditEventType>().HaveConversion<string>().HaveMaxLength(48);

        base.ConfigureConventions(configurationBuilder);
    }
}
