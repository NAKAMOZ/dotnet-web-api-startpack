using Api.Models;
using Api.Models.Enums;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Npgsql.EntityFrameworkCore.PostgreSQL.Infrastructure;

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
public sealed class AppDbContext(DbContextOptions<AppDbContext> options)
    : DbContext(options), IDataProtectionKeyContext
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

    /// <summary>
    /// The Data Protection key ring (ADR-0021) — required by
    /// <see cref="IDataProtectionKeyContext"/>, which is what
    /// <c>PersistKeysToDbContext&lt;AppDbContext&gt;()</c> constrains on.
    /// </summary>
    /// <remarks>
    /// <b>Nothing in this codebase reads it.</b> Data Protection owns the lifecycle
    /// entirely — creating successor keys at runtime as the active one ages. The property
    /// exists so the wiring compiles, and its shape is the package's, not ours.
    /// </remarks>
    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();

    /// <summary>
    /// PostgreSQL schema every table in this model lives in.
    /// </summary>
    /// <remarks>
    /// Not <c>public</c>. The API may share a database with other things, and a dedicated
    /// schema keeps its fourteen tables identifiable as one unit — which is also what makes
    /// a scoped grant possible: the runtime role needs rights on <c>auth</c> and nothing
    /// else, so a mistake in another schema cannot reach the credential store.
    /// </remarks>
    public const string Schema = "auth";

    /// <summary>
    /// Reads the one connection string this application has, failing by name when it is
    /// absent.
    /// </summary>
    /// <remarks>
    /// A missing connection string that surfaces as a 500 on the first login reads like a
    /// database outage; here it names itself. Shared with the design-time factory so EF
    /// tooling and the running host cannot disagree about where the database is.
    /// </remarks>
    public static string ResolveConnectionString(IConfiguration configuration) =>
        configuration.GetConnectionString("Postgres") is { } connectionString
        && !string.IsNullOrWhiteSpace(connectionString)
            ? connectionString
            : throw new InvalidOperationException(
                "Connection string 'Postgres' is not configured. Set ConnectionStrings:Postgres "
                + "in user-secrets or configuration, or the ConnectionStrings__Postgres "
                + "environment variable.");

    /// <summary>
    /// Provider options shared by the runtime context and the design-time factory.
    /// </summary>
    /// <remarks>
    /// Spelled once because EF tooling builds its own <see cref="DbContextOptions"/>: a
    /// second, hand-written provider configuration is how a migration comes to be scaffolded
    /// against a model the running application does not have.
    /// </remarks>
    public static void ConfigureNpgsql(NpgsqlDbContextOptionsBuilder npgsql)
    {
        // Transient faults — a failover, a dropped connection, a restarting container — are
        // retried rather than surfaced as a 500.
        //
        // The cost is real and worth stating up front: with a retrying execution strategy,
        // code that opens its own transaction must run through
        // db.Database.CreateExecutionStrategy().ExecuteAsync(...) or EF throws at runtime.
        // §12's refresh rotation is exactly that shape — DataAccess.md carries the pattern.
        npgsql.EnableRetryOnFailure(
            maxRetryCount: 3,
            maxRetryDelay: TimeSpan.FromSeconds(5),
            errorCodesToAdd: null);

        // The history table lives with the tables it describes. Left in `public` it would sit
        // apart from the schema it versions, and a database hosting two applications would
        // have them competing for one history table.
        npgsql.MigrationsHistoryTable("__EFMigrationsHistory", Schema);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);

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
