using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace IntegrationTests;

/// <summary>
/// The pipeline contract every response is held to (§14): security headers, a correlation
/// id, and one error envelope — including on the responses nobody wrote.
/// </summary>
/// <remarks>
/// Runs as <c>Production</c>, which is the environment the leak guards apply in. §22 owns
/// the adversarial suite; these assertions are the floor that suite builds on.
/// </remarks>
public class PipelineTests : IClassFixture<WebApplicationFactory<Program>>
{
    private const string CorrelationIdHeader = "X-Correlation-Id";

    private readonly WebApplicationFactory<Program> _factory;

    public PipelineTests(WebApplicationFactory<Program> factory) => _factory = factory;

    [Fact]
    public async Task EveryResponseCarriesTheSecurityHeaders()
    {
        // An unauthenticated request to an unknown path: no controller, no action, no code
        // of ours running. The headers still have to be there — the responses easiest to
        // forget are the ones nobody wrote.
        var response = await CreateClient().GetAsync("/", TestContext.Current.CancellationToken);

        Assert.Equal("nosniff", Single(response, "X-Content-Type-Options"));
        Assert.Equal("no-referrer", Single(response, "Referrer-Policy"));
        Assert.Equal("DENY", Single(response, "X-Frame-Options"));
        Assert.Equal("same-origin", Single(response, "Cross-Origin-Resource-Policy"));
        Assert.StartsWith("default-src 'none'", Single(response, "Content-Security-Policy"));
        Assert.NotNull(Single(response, CorrelationIdHeader));
    }

    [Fact]
    public async Task AWellFormedInboundCorrelationIdIsAdopted()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/");
        request.Headers.Add(CorrelationIdHeader, "order-4711.retry_2");

        var response = await CreateClient().SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal("order-4711.retry_2", Single(response, CorrelationIdHeader));
    }

    [Fact]
    public async Task AMalformedInboundCorrelationIdIsReplaced()
    {
        // Header injection through the correlation id is on §22's list. The value reaches
        // log sinks and the audit table, so it is replaced rather than echoed.
        var request = new HttpRequestMessage(HttpMethod.Get, "/");
        request.Headers.TryAddWithoutValidation(CorrelationIdHeader, "forged {\"user\":\"admin\"}");

        var response = await CreateClient().SendAsync(request, TestContext.Current.CancellationToken);

        var echoed = Single(response, CorrelationIdHeader);

        Assert.NotNull(echoed);
        Assert.DoesNotContain("admin", echoed, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AnUnhandledExceptionBecomesAProblemDocumentWithTheHeadersIntact()
    {
        // The regression this test exists for: UseExceptionHandler clears the response —
        // status, headers and body — before writing the problem document. Headers written on
        // the way in would therefore survive on 2xx and vanish on 5xx, leaving the responses
        // that matter most without a policy and without an id to trace them by.
        var client = CreateClient(services =>
            services.Configure<MvcOptions>(options => options.Filters.Add(new ThrowingFilter())));

        var request = new HttpRequestMessage(HttpMethod.Get, "/.well-known/jwks.json");
        request.Headers.Add(CorrelationIdHeader, "probe-500");

        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("nosniff", Single(response, "X-Content-Type-Options"));
        Assert.Equal("probe-500", Single(response, CorrelationIdHeader));

        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(
            TestContext.Current.CancellationToken);

        Assert.Equal("internal_error", problem.GetProperty("errorCode").GetString());
        Assert.Equal("probe-500", problem.GetProperty("correlationId").GetString());

        // Outside Development the fault detail and the framework's `exception` extension are
        // both withheld: a stack trace names internal paths, dependency versions and query
        // shapes, and an exception message may carry a connection string outright.
        Assert.False(problem.TryGetProperty("detail", out _));
        Assert.False(problem.TryGetProperty("exception", out _));
    }

    [Fact]
    public async Task AnUnknownOriginGetsNoCorsGrant()
    {
        // Both allowlists are empty in this configuration, so no origin is allowed — an
        // unconfigured deployment is closed rather than open.
        var request = new HttpRequestMessage(HttpMethod.Options, "/");
        request.Headers.Add("Origin", "https://evil.example");
        request.Headers.Add("Access-Control-Request-Method", "POST");

        var response = await CreateClient().SendAsync(request, TestContext.Current.CancellationToken);

        Assert.False(response.Headers.Contains("Access-Control-Allow-Origin"));
    }

    private HttpClient CreateClient(Action<IServiceCollection>? configureServices = null) =>
        _factory
            .WithWebHostBuilder(builder =>
            {
                // Production, for the same reason as the composition-root smoke test: it is
                // the environment the leak guards apply in, and it turns the migrate-and-seed
                // startup step into the no-op it is outside Development. Nothing here ever
                // connects, so the connection string only has to exist.
                builder.UseEnvironment("Production");
                builder.UseSetting(
                    "ConnectionStrings:Postgres",
                    "Host=localhost;Database=pipeline-tests");

                if (configureServices is not null)
                {
                    builder.ConfigureTestServices(configureServices);
                }
            })
            .CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    private static string? Single(HttpResponseMessage response, string header) =>
        response.Headers.TryGetValues(header, out var values) ? values.Single() : null;

    /// <summary>
    /// Throws before the action runs, so the fault is a pipeline fault rather than anything
    /// the endpoint did — and so the JWKS action never reaches the database that is not there.
    /// </summary>
    private sealed class ThrowingFilter : IActionFilter, IOrderedFilter
    {
        /// <summary>Runs before every other filter, including the validation filter.</summary>
        public int Order => int.MinValue;

        public void OnActionExecuting(ActionExecutingContext context) =>
            throw new InvalidOperationException("Host=db;Password=hunter2");

        public void OnActionExecuted(ActionExecutedContext context)
        {
        }
    }
}
