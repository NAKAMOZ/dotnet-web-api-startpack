using Api.Data;
using IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;

namespace IntegrationTests;

[Collection(IntegrationTestCollection.Name)]
public sealed class PostgresInfrastructureTests(IntegrationTestFactory factory)
{
    [Fact]
    public async Task HealthProbes_ReportLiveAndDatabaseReady()
    {
        var client = factory.CreateClient();

        var live = await client.GetAsync(
            "/health/live",
            TestContext.Current.CancellationToken);
        var ready = await client.GetAsync(
            "/health/ready",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, live.StatusCode);
        Assert.Equal(HttpStatusCode.OK, ready.StatusCode);
        Assert.Equal("Healthy", await live.Content.ReadAsStringAsync(
            TestContext.Current.CancellationToken));
        Assert.Equal("Healthy", await ready.Content.ReadAsStringAsync(
            TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task DependencyFailure_FailsReadinessWithoutFailingLivenessOrLeakingDetails()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var postgres = IntegrationTestFactory.CreateContainer("health_failure_tests");
        await postgres.StartAsync(cancellationToken);

        using var isolatedFactory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
                IntegrationTestFactory.ApplyTestingSettings(
                    builder,
                    postgres.GetConnectionString()));
        var client = isolatedFactory.CreateClient();

        await using (var scope = isolatedFactory.Services.CreateAsyncScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await database.Database.MigrateAsync(cancellationToken);
        }

        await postgres.StopAsync(cancellationToken);

        var live = await client.GetAsync("/health/live", cancellationToken);
        var ready = await client.GetAsync("/health/ready", cancellationToken);

        Assert.Equal(HttpStatusCode.OK, live.StatusCode);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, ready.StatusCode);
        Assert.Equal("Healthy", await live.Content.ReadAsStringAsync(cancellationToken));
        Assert.Equal("Unhealthy", await ready.Content.ReadAsStringAsync(cancellationToken));
    }

    [Fact]
    public async Task Migrations_CreateAuthSchemaCitextAndReferenceRoles()
    {
        await factory.ResetDatabaseAsync();

        await factory.InScopeAsync(async services =>
        {
            var database = services.GetRequiredService<AppDbContext>();

            var migrations = await database.Database.GetAppliedMigrationsAsync(
                TestContext.Current.CancellationToken);
            var roles = await database.Roles
                .AsNoTracking()
                .Select(role => role.Name)
                .OrderBy(name => name)
                .ToListAsync(TestContext.Current.CancellationToken);
            var citext = await database.Database
                .SqlQueryRaw<string>("SELECT extname AS \"Value\" FROM pg_extension WHERE extname = 'citext'")
                .SingleAsync(TestContext.Current.CancellationToken);

            Assert.Equal(database.Database.GetMigrations(), migrations);
            Assert.Equal(["Admin", "User"], roles);
            Assert.Equal("citext", citext);
        });
    }

    [Fact]
    public async Task Reset_RemovesApplicationRowsButPreservesMigrationsAndRoles()
    {
        await factory.ResetDatabaseAsync();

        await factory.InScopeAsync(async services =>
        {
            var database = services.GetRequiredService<AppDbContext>();
            database.Users.Add(new()
            {
                Email = "reset@example.com",
            });
            await database.SaveChangesAsync(TestContext.Current.CancellationToken);
        });

        await factory.ResetDatabaseAsync();

        await factory.InScopeAsync(async services =>
        {
            var database = services.GetRequiredService<AppDbContext>();
            Assert.False(await database.Users.AnyAsync(TestContext.Current.CancellationToken));
            Assert.Equal(2, await database.Roles.CountAsync(TestContext.Current.CancellationToken));
        });
    }
}
