using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace IntegrationTests;

/// <summary>
/// Proves the composition root actually builds a working host: every
/// <c>Add*</c> extension resolves and the pipeline starts (§3).
/// This remains the fast, database-free composition smoke. §21's separate collection owns
/// the Testcontainers-backed migration and persistence tests.
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
            builder.UseProductionLikeHost("Production", "composition-root-smoke-test");
        });

        var client = factory.CreateClient();

        var response = await client.GetAsync("/", TestContext.Current.CancellationToken);

        // 401, not 404 — and that is the assertion worth making as of §12.
        //
        // The deny-by-default fallback applies to requests matching NO endpoint as well as
        // to endpoints carrying no authorization metadata. An unknown path therefore answers
        // "authenticate first" rather than "no such path", so an anonymous caller learns
        // nothing about which paths exist.
        //
        // Reaching any status at all still proves what this test was written for: the host
        // booted, every Add* extension resolved, and the pipeline ran.
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
