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
        var client = _factory.CreateClient();

        // No controllers exist yet, so any route 404s. Reaching a 404 at all means the
        // host booted, DI resolved, and the pipeline ran — which is what is under test.
        var response = await client.GetAsync("/", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
