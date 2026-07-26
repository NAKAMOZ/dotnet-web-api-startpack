using System.Diagnostics;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Api.Data;
using Api.Data.Seeding;
using Api.DTOs.Admin;
using Api.DTOs.ApiKeys;
using Api.DTOs.Auth;
using Api.DTOs.PasswordReset;
using Api.Handlers.Authorization;
using Api.Models;
using Api.Services.Crypto;
using IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace IntegrationTests.Security;

[Collection(IntegrationTestCollection.Name)]
[Trait("Category", "Security")]
public sealed class FeatureAttackTests(IntegrationTestFactory factory)
{
    [Fact]
    public async Task EnumerationPaths_ExposeTheSamePublicOutcomes()
    {
        await factory.ResetAsync();
        const string email = "enumeration@example.com";
        const string password = "V4lid!River-Stone-Cobalt-47";
        await SeedUserAsync(email, password, RoleSeed.UserRoleId);
        var client = factory.CreateClient();

        var knownReset = await client.PostAsJsonAsync(
            "/api/v1/password-reset/request",
            new PasswordResetRequest { Email = email },
            TestContext.Current.CancellationToken);
        var unknownReset = await client.PostAsJsonAsync(
            "/api/v1/password-reset/request",
            new PasswordResetRequest { Email = "absent@example.com" },
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Accepted, knownReset.StatusCode);
        Assert.Equal(knownReset.StatusCode, unknownReset.StatusCode);
        Assert.Equal(
            await knownReset.Content.ReadAsStringAsync(TestContext.Current.CancellationToken),
            await unknownReset.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));

        var known = await TimedLoginAsync(client, email, "wrong password");
        var unknown = await TimedLoginAsync(client, "absent@example.com", "wrong password");

        Assert.Equal(HttpStatusCode.Unauthorized, known.Response.StatusCode);
        Assert.Equal(known.Response.StatusCode, unknown.Response.StatusCode);
        Assert.Equal(await PublicErrorAsync(known.Response), await PublicErrorAsync(unknown.Response));

        // A generous bound catches a skipped Argon2 verification (orders of magnitude)
        // without pretending a shared CI runner can provide microbenchmark precision.
        var slower = Math.Max(known.Elapsed.TotalMilliseconds, unknown.Elapsed.TotalMilliseconds);
        var faster = Math.Max(1, Math.Min(known.Elapsed.TotalMilliseconds, unknown.Elapsed.TotalMilliseconds));
        Assert.True(slower / faster < 4, $"Enumeration timing ratio was {slower / faster:F2}.");
    }

    [Fact]
    public async Task Lockout_IsInvisibleAndAdminUnlockRestoresLogin()
    {
        await factory.ResetAsync();
        const string email = "lockout@example.com";
        const string password = "V4lid!River-Stone-Cobalt-47";
        var targetUserId = await SeedUserAsync(email, password, RoleSeed.UserRoleId);
        var client = factory.CreateClient();

        for (var attempt = 0; attempt < 5; attempt++)
        {
            var failed = await client.PostAsJsonAsync(
                "/api/v1/auth/login",
                new LoginRequest { Email = email, Password = "wrong password" },
                TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.Unauthorized, failed.StatusCode);
        }

        var locked = await client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new LoginRequest { Email = email, Password = password },
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Unauthorized, locked.StatusCode);
        Assert.Equal("invalid_credentials", (await PublicErrorAsync(locked)).Code);

        var adminUserId = await SeedUserAsync(
            "unlock-admin@example.com",
            password,
            RoleSeed.AdminRoleId);
        var adminToken = await factory.IssueAccessTokenAsync(
            adminUserId,
            Guid.CreateVersion7(),
            TestContext.Current.CancellationToken,
            [Roles.Admin]);
        using var unlock = new HttpRequestMessage(
            HttpMethod.Patch,
            $"/api/v1/admin/users/{targetUserId}")
        {
            Content = JsonContent.Create(new AdminUpdateUserRequest { Unlock = true }),
        };
        unlock.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        var unlocked = await client.SendAsync(unlock, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, unlocked.StatusCode);

        var success = await client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new LoginRequest { Email = email, Password = password },
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, success.StatusCode);
    }

    [Fact]
    public async Task ApiKeyScopes_AreIntersectedWithTheOwnersCurrentRoles()
    {
        await factory.ResetAsync();
        var userId = await SeedUserAsync(
            "api-key-admin@example.com",
            "V4lid!River-Stone-Cobalt-47",
            RoleSeed.AdminRoleId);
        var accessToken = await factory.IssueAccessTokenAsync(
            userId,
            Guid.CreateVersion7(),
            TestContext.Current.CancellationToken,
            [Roles.Admin]);
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        var created = await client.PostAsJsonAsync(
            "/api/v1/api-keys",
            new CreateApiKeyRequest
            {
                Name = "incident-reader",
                Scopes = [Permissions.UsersReadAny],
            },
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var key = await created.Content.ReadFromJsonAsync<CreateApiKeyResponse>(
            TestContext.Current.CancellationToken);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("ApiKey", key!.Key);
        var permitted = await client.GetAsync(
            "/api/v1/admin/users",
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, permitted.StatusCode);

        await factory.InScopeAsync(async services =>
        {
            var database = services.GetRequiredService<AppDbContext>();
            var assignment = await database.UserRoles.SingleAsync(
                userRole => userRole.UserId == userId,
                TestContext.Current.CancellationToken);
            database.UserRoles.Remove(assignment);
            database.UserRoles.Add(new UserRole { UserId = userId, RoleId = RoleSeed.UserRoleId });
            await database.SaveChangesAsync(TestContext.Current.CancellationToken);
        });

        var denied = await client.GetAsync(
            "/api/v1/admin/users",
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
    }

    private async Task<Guid> SeedUserAsync(string email, string password, Guid roleId)
    {
        var userId = Guid.CreateVersion7();
        await factory.InScopeAsync(async services =>
        {
            var database = services.GetRequiredService<AppDbContext>();
            var hasher = services.GetRequiredService<IPasswordHasher>();
            database.Users.Add(new User
            {
                Id = userId,
                Email = email,
                EmailVerified = true,
                PasswordHash = hasher.Hash(password),
            });
            database.UserRoles.Add(new UserRole { UserId = userId, RoleId = roleId });
            await database.SaveChangesAsync(TestContext.Current.CancellationToken);
        });
        return userId;
    }

    private static async Task<(HttpResponseMessage Response, TimeSpan Elapsed)> TimedLoginAsync(
        HttpClient client,
        string email,
        string password)
    {
        var stopwatch = Stopwatch.StartNew();
        var response = await client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new LoginRequest { Email = email, Password = password },
            TestContext.Current.CancellationToken);
        stopwatch.Stop();
        return (response, stopwatch.Elapsed);
    }

    private static async Task<(string? Code, string? Title)> PublicErrorAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(
            TestContext.Current.CancellationToken);
        return (
            body.GetProperty("errorCode").GetString(),
            body.GetProperty("title").GetString());
    }
}
