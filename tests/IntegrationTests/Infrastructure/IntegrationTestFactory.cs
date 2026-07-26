using Api.BackgroundServices;
using Api.Data;
using Api.Models;
using Api.Models.Enums;
using Api.Services.Tokens;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Time.Testing;
using Respawn;
using Respawn.Graph;
using Testcontainers.PostgreSql;

namespace IntegrationTests.Infrastructure;

/// <summary>
/// One real PostgreSQL container and one application host for the full integration
/// collection. Schema creation always uses EF migrations; Respawn clears application data
/// without rebuilding the container between tests.
/// </summary>
public sealed class IntegrationTestFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    /// <summary>
    /// The image every integration container runs, named once so a major-version bump is
    /// one edit rather than a search.
    /// </summary>
    public const string PostgresImage = "postgres:18-alpine";

    private readonly PostgreSqlContainer _postgres = CreateContainer("integration_tests");

    private Respawner? _respawner;

    public FakeTimeProvider Clock { get; } = new(DateTimeOffset.UtcNow);

    /// <summary>
    /// A container matching the fixture's own, for the rare test that needs to break one.
    /// </summary>
    public static PostgreSqlContainer CreateContainer(string database) =>
        new PostgreSqlBuilder(PostgresImage)
            .WithDatabase(database)
            .WithUsername("postgres")
            .WithPassword("postgres")
            .Build();

    /// <summary>
    /// The host settings that make a Testing host boot, shared with isolated factories.
    /// </summary>
    public static void ApplyTestingSettings(IWebHostBuilder builder, string connectionString)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Postgres", connectionString);
        builder.UseSetting("AuthCookies:RequireSecure", "false");
    }

    public async ValueTask InitializeAsync()
    {
        await _postgres.StartAsync();

        // Building the client starts the TestServer. Testing is intentionally not
        // Development: the fixture, not application startup, owns migration timing.
        _ = CreateClient();

        await using var scope = Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await database.Database.MigrateAsync();
        await database.Database.OpenConnectionAsync();

        _respawner = await Respawner.CreateAsync(
            database.Database.GetDbConnection(),
            new RespawnerOptions
            {
                DbAdapter = DbAdapter.Postgres,
                SchemasToInclude = [AppDbContext.Schema],
                TablesToIgnore =
                [
                    new Table(AppDbContext.Schema, "__EFMigrationsHistory"),
                    new Table(AppDbContext.Schema, "Roles"),
                ],
            });
    }

    public new async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        await _postgres.DisposeAsync();
    }

    /// <summary>
    /// Returns the collection to a clean starting point: application rows gone, clock moved
    /// past every instant the previous test observed.
    /// </summary>
    /// <remarks>
    /// The clock only ever moves forward. Rewinding it to a fixed baseline would read better,
    /// but the shared host's in-memory caches — the signing-key ring among them — outlive
    /// Respawn, and an entry cached as valid until some future instant would still be cached
    /// after time travelled back behind the rows it describes.
    /// </remarks>
    public async Task ResetAsync()
    {
        await ResetDatabaseAsync();
        Clock.Advance(TimeSpan.FromTicks(1));
    }

    public async Task ResetDatabaseAsync()
    {
        if (_respawner is null)
        {
            throw new InvalidOperationException("The integration fixture has not initialized.");
        }

        await using var scope = Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await database.Database.OpenConnectionAsync();
        await _respawner.ResetAsync(database.Database.GetDbConnection());
    }

    public async Task<T> InScopeAsync<T>(Func<IServiceProvider, Task<T>> action)
    {
        await using var scope = Services.CreateAsyncScope();
        return await action(scope.ServiceProvider);
    }

    public async Task InScopeAsync(Func<IServiceProvider, Task> action)
    {
        await using var scope = Services.CreateAsyncScope();
        await action(scope.ServiceProvider);
    }

    /// <summary>Persists a minimal verified user and returns its id.</summary>
    public async Task<Guid> SeedUserAsync(CancellationToken cancellationToken)
    {
        var userId = Guid.CreateVersion7();

        await InScopeAsync(async services =>
        {
            var database = services.GetRequiredService<AppDbContext>();
            database.Users.Add(new User
            {
                Id = userId,
                Email = $"{userId:N}@example.com",
                EmailVerified = true,
            });
            await database.SaveChangesAsync(cancellationToken);
        });

        return userId;
    }

    /// <summary>
    /// Issues a real access token for an arbitrary subject without running a full login.
    /// The token is signed by the host's own key ring, so isolated endpoint scenarios still
    /// exercise the same validation path as a genuine login.
    /// </summary>
    public async Task<string> IssueAccessTokenAsync(
        Guid userId,
        Guid sessionId,
        CancellationToken cancellationToken,
        IReadOnlyList<string>? roles = null)
    {
        var issued = await InScopeAsync(services =>
            services.GetRequiredService<IAccessTokenIssuer>().IssueAsync(
                new AccessTokenRequest
                {
                    UserId = userId,
                    SessionId = sessionId,
                    EmailVerified = true,
                    Roles = roles ?? ["User"],
                    AuthenticationMethods = [AuthenticationMethod.Password],
                    AuthenticatedAt = Clock.GetUtcNow(),
                },
                cancellationToken));

        return issued.Value;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        ApplyTestingSettings(builder, _postgres.GetConnectionString());
        builder.UseSetting("RateLimiting:GeneralPermitLimit", "100000");
        builder.UseSetting("RateLimiting:AuthStrictPermitLimit", "10000");
        builder.UseSetting("RateLimiting:EmailSendingIpPermitLimit", "10000");
        builder.UseSetting("RateLimiting:EmailSendingAccountPermitLimit", "10000");
        builder.UseSetting("RateLimiting:RegistrationPermitLimit", "10000");

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(Clock);

            // A shared FakeTimeProvider is advanced by expiry tests. Letting the real
            // periodic cleanup worker observe those jumps makes it delete another test's
            // deliberately expired token in the background. Keep the same concrete worker
            // available for its explicit integration test, but do not schedule it in the
            // Testing host.
            var cleanupRegistration = services.Single(descriptor =>
                descriptor.ServiceType == typeof(IHostedService)
                && descriptor.ImplementationType == typeof(ExpiredAuthArtifactCleanupService));
            services.Remove(cleanupRegistration);
            services.AddSingleton<ExpiredAuthArtifactCleanupService>();
        });
    }
}
