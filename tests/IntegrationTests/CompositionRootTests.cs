using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace IntegrationTests;

/// <summary>
/// Proves the composition root actually builds a working host: every
/// <c>Add*</c> extension resolves and the pipeline starts (§3).
/// This is a skeleton smoke test, not a feature test — §21 replaces it with the
/// Testcontainers-backed fixture once there is a database to talk to.
/// </summary>
public class CompositionRootTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public CompositionRootTests(WebApplicationFactory<Program> factory) => _factory = factory;

    [Fact]
    public async Task ApplicationStarts()
    {
        var factory = _factory.WithWebHostBuilder(builder =>
        {
            // WebApplicationFactory defaults to Development, and Development migrates and
            // seeds at startup (§8). This test asserts that the composition root resolves
            // and the pipeline runs — it is not a database test, and making it one would
            // mean every unit-test run needed a live PostgreSQL. Overriding the environment
            // turns UseDatabaseSetupAsync into the no-op it is everywhere but Development.
            // §21 covers the migrate-and-seed path against a Testcontainers instance.
            builder.UseEnvironment("Production");

            // Still required: §7 fails the boot when no connection string is configured, and
            // no committed file carries one. Nothing ever connects through it here.
            builder.UseSetting(
                "ConnectionStrings:Postgres",
                "Host=localhost;Database=composition-root-smoke-test");
        });

        var client = factory.CreateClient();

        // No controllers exist yet, so any route 404s. Reaching a 404 at all means the
        // host booted, DI resolved, and the pipeline ran — which is what is under test.
        var response = await client.GetAsync("/", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
