using Api.Handlers.Authorization;
using Api.Models;
using Api.Models.Enums;
using Api.Services.Crypto;
using Microsoft.EntityFrameworkCore;

namespace Api.Data.Seeding;

/// <summary>
/// Creates two known accounts — one admin, one ordinary user — so a fresh clone has
/// something to log in as. <b>Development only.</b>
/// </summary>
/// <remarks>
/// Two independent guards stand between this and a production database, because one is not
/// enough for code that creates accounts with published passwords:
/// <list type="number">
/// <item>the caller (<c>UseDatabaseSetup</c>) only invokes it in Development;</item>
/// <item>this class checks the environment again and refuses regardless of who called it.</item>
/// </list>
/// The second guard is what survives a future refactor that moves the call site.
/// </remarks>
public sealed class DevDataSeeder(
    AppDbContext dbContext,
    IHostEnvironment environment,
    ILogger<DevDataSeeder> logger,
    TimeProvider timeProvider,
    IPasswordHasher? passwordHasher = null) : IDataSeeder
{
    /// <summary>
    /// Obviously fake, and deliberately so — a plausible-looking password invites reuse.
    /// Published in the runbook, logged at startup, and reachable only on a database that
    /// was seeded in Development.
    /// </summary>
    private const string AdminPassword = "Dev_Admin_Password_1!";

    private const string UserPassword = "Dev_User_Password_1!";

    private static readonly Guid AdminUserId = new("0198f3a0-0000-7000-8001-000000000001");

    private static readonly Guid RegularUserId = new("0198f3a0-0000-7000-8001-000000000002");

    private const string DemoApiKeyPrefix = "demoAdmin01";

    private const string DemoApiKeySecret = "Dev_Demo_Api_Key_Only_Local_2026";

    public const string DemoApiKey = $"ak_{DemoApiKeyPrefix}_{DemoApiKeySecret}";

    public async Task SeedAsync(CancellationToken cancellationToken)
    {
        if (!environment.IsDevelopment())
        {
            logger.LogWarning(
                "Development data seeder invoked in environment {Environment}. Refusing to run.",
                environment.EnvironmentName);
            return;
        }

        // §12 owns Argon2PasswordHasher. Until it is registered, seed the accounts without
        // passwords rather than with a placeholder: a fake hash that verifies against a
        // known string is a backdoor, and one that verifies against nothing is a login bug
        // nobody can diagnose. A passwordless account is a state the model already supports
        // (ADR-0006) and one that no code path can authenticate.
        if (passwordHasher is null)
        {
            logger.LogWarning(
                "No IPasswordHasher is registered (§12). Seeding development accounts without " +
                "passwords — they cannot be logged into until the hasher lands.");
        }

        await SeedUserAsync(
            AdminUserId,
            "admin@localhost.dev",
            "Dev Admin",
            AdminPassword,
            RoleSeed.AdminRoleId,
            cancellationToken);

        await SeedUserAsync(
            RegularUserId,
            "user@localhost.dev",
            "Dev User",
            UserPassword,
            RoleSeed.UserRoleId,
            cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);
        await SeedFixturesAsync(cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        // Loud on purpose. A developer who cannot tell whether the running database carries
        // seeded credentials will assume it does not.
        logger.LogWarning(
            "Development fixtures seeded: {AdminEmail} / {AdminPassword}, {UserEmail} / {UserPassword}, " +
            "and API key {DemoApiKey}. These exist only in Development.",
            "admin@localhost.dev",
            AdminPassword,
            "user@localhost.dev",
            UserPassword,
            DemoApiKey);
    }

    private async Task SeedFixturesAsync(CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var regularUser = await dbContext.Users
            .AsNoTracking()
            .SingleAsync(user => user.Id == RegularUserId, cancellationToken);

        await AddSessionIfMissingAsync(
            new Guid("0198f3a0-0000-7000-8001-000000000101"),
            regularUser.SecurityStamp,
            "Safari on iPhone",
            "127.0.0.1",
            now - TimeSpan.FromMinutes(18),
            now,
            cancellationToken);
        await AddSessionIfMissingAsync(
            new Guid("0198f3a0-0000-7000-8001-000000000102"),
            regularUser.SecurityStamp,
            "Firefox on Linux",
            "192.0.2.42",
            now - TimeSpan.FromHours(2),
            now,
            cancellationToken);

        if (!await dbContext.Accounts.AnyAsync(
                account => account.Id == new Guid("0198f3a0-0000-7000-8001-000000000401"),
                cancellationToken))
        {
            dbContext.Accounts.Add(new Account
            {
                Id = new Guid("0198f3a0-0000-7000-8001-000000000401"),
                UserId = RegularUserId,
                Provider = "github",
                ProviderAccountId = "development-linked-user",
            });
        }

        if (passwordHasher is not null
            && !await dbContext.ApiKeys.AnyAsync(
                key => key.Id == new Guid("0198f3a0-0000-7000-8001-000000000301"),
                cancellationToken))
        {
            dbContext.ApiKeys.Add(new ApiKey
            {
                Id = new Guid("0198f3a0-0000-7000-8001-000000000301"),
                UserId = AdminUserId,
                Name = "Development workbench",
                KeyPrefix = DemoApiKeyPrefix,
                KeyHash = passwordHasher.HashSecret(DemoApiKeySecret),
                Scopes = [.. Permissions.All],
                ExpiresAt = now + TimeSpan.FromDays(365),
            });
        }

        var auditFixtures = new[]
        {
            DemoAudit(
                "0198f3a0-0000-7000-8001-000000000501",
                AdminUserId,
                AuditEventType.LoginSucceeded,
                now - TimeSpan.FromMinutes(34),
                """{"source":"development-seeder","method":"password"}"""),
            DemoAudit(
                "0198f3a0-0000-7000-8001-000000000502",
                RegularUserId,
                AuditEventType.TokenRefreshed,
                now - TimeSpan.FromMinutes(22),
                """{"source":"development-seeder"}"""),
            DemoAudit(
                "0198f3a0-0000-7000-8001-000000000503",
                RegularUserId,
                AuditEventType.ApiKeyCreated,
                now - TimeSpan.FromMinutes(12),
                """{"source":"development-seeder","name":"Local automation"}"""),
        };
        var fixtureIds = auditFixtures.Select(fixture => fixture.Id).ToArray();
        var existingAuditIds = await dbContext.AuditLogEntries
            .Where(entry => fixtureIds.Contains(entry.Id))
            .Select(entry => entry.Id)
            .ToListAsync(cancellationToken);
        dbContext.AuditLogEntries.AddRange(
            auditFixtures.Where(fixture => !existingAuditIds.Contains(fixture.Id)));
    }

    private async Task AddSessionIfMissingAsync(
        Guid id,
        string securityStamp,
        string device,
        string ipAddress,
        DateTimeOffset lastActiveAt,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (await dbContext.Sessions.AnyAsync(session => session.Id == id, cancellationToken))
        {
            return;
        }

        dbContext.Sessions.Add(new Session
        {
            Id = id,
            UserId = RegularUserId,
            IpAddress = ipAddress,
            UserAgent = $"Demo fixture — {device}",
            DeviceLabel = device,
            AuthenticationMethods = [AuthenticationMethod.Password],
            SecurityStamp = securityStamp,
            AuthenticatedAt = lastActiveAt,
            LastActiveAt = lastActiveAt,
            AbsoluteExpiresAt = now + TimeSpan.FromDays(7),
        });
    }

    private static AuditLogEntry DemoAudit(
        string id,
        Guid userId,
        AuditEventType eventType,
        DateTimeOffset occurredAt,
        string metadata) =>
        new()
        {
            Id = new Guid(id),
            UserId = userId,
            EventType = eventType,
            IpAddress = "127.0.0.1",
            UserAgent = "Development workbench fixture",
            CorrelationId = $"demo-{id[^3..]}",
            Metadata = metadata,
            OccurredAt = occurredAt,
        };

    private async Task SeedUserAsync(
        Guid userId,
        string email,
        string displayName,
        string password,
        Guid roleId,
        CancellationToken cancellationToken)
    {
        // Idempotent by id, not by email: the development loop restarts the app constantly
        // and only occasionally drops the database.
        var existing = await dbContext.Users.SingleOrDefaultAsync(
            user => user.Id == userId,
            cancellationToken);

        if (existing is not null)
        {
            if (existing.PasswordHash is null && passwordHasher is not null)
            {
                existing.PasswordHash = passwordHasher.Hash(password);
            }

            return;
        }

        dbContext.Users.Add(new User
        {
            Id = userId,
            Email = email,
            DisplayName = displayName,
            EmailVerified = true,
            PasswordHash = passwordHasher?.Hash(password),
        });

        dbContext.UserRoles.Add(new UserRole { UserId = userId, RoleId = roleId });
    }
}
