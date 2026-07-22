using Api.Models;
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

        // Loud on purpose. A developer who cannot tell whether the running database carries
        // seeded credentials will assume it does not.
        logger.LogWarning(
            "Development accounts seeded: {AdminEmail} / {AdminPassword} and {UserEmail} / {UserPassword}. " +
            "These exist only in Development.",
            "admin@localhost.dev",
            AdminPassword,
            "user@localhost.dev",
            UserPassword);
    }

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
        if (await dbContext.Users.AnyAsync(user => user.Id == userId, cancellationToken))
        {
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
