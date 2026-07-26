using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using Api.Configuration;
using Api.Data;
using Api.Data.Seeding;
using Api.DTOs.Auth;
using Api.Models;
using Api.Models.Enums;
using Api.Services.Audit;
using Api.Services.Crypto;
using Api.Services.Tokens;
using IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace IntegrationTests;

[Collection(IntegrationTestCollection.Name)]
public sealed class DevelopmentFixturesTests(IntegrationTestFactory factory)
{
    [Fact]
    public async Task DevelopmentSeeder_CreatesUsableWorkbenchFixtures()
    {
        await factory.ResetAsync();

        await factory.InScopeAsync(async services =>
        {
            var seeder = new DevDataSeeder(
                services.GetRequiredService<AppDbContext>(),
                new DevelopmentEnvironment(),
                services.GetRequiredService<ILogger<DevDataSeeder>>(),
                services.GetRequiredService<TimeProvider>(),
                services.GetRequiredService<IPasswordHasher>());

            await seeder.SeedAsync(TestContext.Current.CancellationToken);
        });

        await factory.InScopeAsync(async services =>
        {
            var database = services.GetRequiredService<AppDbContext>();

            Assert.Equal(2, await database.Users.CountAsync(TestContext.Current.CancellationToken));
            Assert.Equal(2, await database.Sessions.CountAsync(TestContext.Current.CancellationToken));
            Assert.Single(await database.Accounts.ToListAsync(TestContext.Current.CancellationToken));
            Assert.Single(await database.ApiKeys.ToListAsync(TestContext.Current.CancellationToken));
            Assert.Equal(
                3,
                await database.AuditLogEntries.CountAsync(TestContext.Current.CancellationToken));
        });

        var login = await factory.CreateClient().PostAsJsonAsync(
            "/api/v1/auth/login",
            new LoginRequest
            {
                Email = "user@localhost.dev",
                Password = "Dev_User_Password_1!",
            },
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, login.StatusCode);

        var apiKeyClient = factory.CreateClient();
        apiKeyClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("ApiKey", DevDataSeeder.DemoApiKey);
        var adminUsers = await apiKeyClient.GetAsync(
            "/api/v1/admin/users",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, adminUsers.StatusCode);
    }

    [Fact]
    public async Task SigningKeyManager_DevelopmentRepairsAnOrphanedActiveKey()
    {
        await factory.ResetAsync();

        await factory.InScopeAsync(async services =>
        {
            var database = services.GetRequiredService<AppDbContext>();
            using var orphanedKeyPair = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            const string orphanedKeyId = "orphaned-development-key";
            database.SigningKeys.Add(new SigningKey
            {
                KeyId = orphanedKeyId,
                PublicKey = Convert.ToBase64String(orphanedKeyPair.ExportSubjectPublicKeyInfo()),
                PrivateKeyProtected = "orphaned-data-protection-payload",
                Status = SigningKeyStatus.Active,
                ActivatedAt = factory.Clock.GetUtcNow(),
            });
            await database.SaveChangesAsync(TestContext.Current.CancellationToken);

            var manager = new SigningKeyManager(
                database,
                services.GetRequiredService<IDataProtectionProvider>(),
                services.GetRequiredService<IOptions<JwtOptions>>(),
                services.GetRequiredService<TimeProvider>(),
                services.GetRequiredService<HybridCache>(),
                services.GetRequiredService<ILogger<SigningKeyManager>>(),
                services.GetRequiredService<IAuditLogger>(),
                new DevelopmentEnvironment());
            var signature = await manager.SignAsync(
                Encoding.UTF8.GetBytes("development-repair-probe"),
                TestContext.Current.CancellationToken);
            var keys = await database.SigningKeys
                .AsNoTracking()
                .ToDictionaryAsync(
                    key => key.KeyId,
                    TestContext.Current.CancellationToken);

            Assert.NotEqual(orphanedKeyId, signature.KeyId);
            Assert.Equal(SigningKeyStatus.Retiring, keys[orphanedKeyId].Status);
            Assert.Equal(SigningKeyStatus.Active, keys[signature.KeyId].Status);
        });
    }

    private sealed class DevelopmentEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;

        public string ApplicationName { get; set; } = "IntegrationTests";

        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
