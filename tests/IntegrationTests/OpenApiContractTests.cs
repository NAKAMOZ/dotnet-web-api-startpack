using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace IntegrationTests;

public class OpenApiContractTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public OpenApiContractTests(WebApplicationFactory<Program> factory) => _factory = factory;

    [Fact]
    public async Task Staging_ExposesV1DocumentAndScalar()
    {
        var client = CreateClient("Staging");

        var openApi = await client.GetAsync(
            "/openapi/v1.json",
            TestContext.Current.CancellationToken);
        var scalar = await client.GetAsync(
            "/scalar/v1",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, openApi.StatusCode);
        Assert.Equal("application/json", openApi.Content.Headers.ContentType?.MediaType);
        Assert.Equal(HttpStatusCode.OK, scalar.StatusCode);
        Assert.Equal("text/html", scalar.Content.Headers.ContentType?.MediaType);

        var html = await scalar.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.DoesNotContain("cdn.jsdelivr.net", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("fonts.googleapis.com", html, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Production_DoesNotExposeOpenApiOrScalar()
    {
        var client = CreateClient("Production");

        // Deny-by-default deliberately turns an unknown anonymous path into 401 rather than
        // 404. The important assertion is that neither documentation endpoint is mapped.
        Assert.Equal(
            HttpStatusCode.Unauthorized,
            (await client.GetAsync(
                "/openapi/v1.json",
                TestContext.Current.CancellationToken)).StatusCode);

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            (await client.GetAsync(
                "/scalar/v1",
                TestContext.Current.CancellationToken)).StatusCode);
    }

    [Fact]
    public async Task Workbench_IsAvailableOutsideProductionOnly()
    {
        var staging = await CreateClient("Staging").GetAsync(
            "/playground/",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, staging.StatusCode);
        Assert.Equal("text/html", staging.Content.Headers.ContentType?.MediaType);
        Assert.Contains(
            "API Workbench",
            await staging.Content.ReadAsStringAsync(TestContext.Current.CancellationToken),
            StringComparison.Ordinal);
        Assert.StartsWith(
            "default-src 'self'",
            staging.Headers.GetValues("Content-Security-Policy").Single());

        var production = await CreateClient("Production").GetAsync(
            "/playground/",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, production.StatusCode);
    }

    [Fact]
    public async Task WorkbenchCatalog_CoversEveryOpenApiOperationAndHealthProbe()
    {
        var document = await CreateClient("Staging").GetFromJsonAsync<JsonElement>(
            "/openapi/v1.json",
            TestContext.Current.CancellationToken);
        var openApiOperations = document
            .GetProperty("paths")
            .EnumerateObject()
            .SelectMany(path => path.Value
                .EnumerateObject()
                .Where(operation => operation.Name is "get" or "post" or "put" or "patch" or "delete")
                .Select(operation => $"{operation.Name.ToUpperInvariant()} {path.Name}"))
            .ToHashSet(StringComparer.Ordinal);
        var script = await File.ReadAllTextAsync(
            Path.Combine(RepositoryPaths.Root, "wwwroot", "playground", "app.js"),
            TestContext.Current.CancellationToken);
        var workbenchOperations = Regex
            .Matches(
                script,
                "endpoint\\(\"[^\"]+\", \"(?<method>GET|POST|PUT|PATCH|DELETE)\", \"(?<path>/[^\"]+)\"")
            .Select(match => $"{match.Groups["method"].Value} {match.Groups["path"].Value}")
            .ToHashSet(StringComparer.Ordinal);

        Assert.True(
            openApiOperations.IsSubsetOf(workbenchOperations),
            $"Workbench is missing: [{string.Join(", ", openApiOperations.Except(workbenchOperations))}].");
        Assert.Equal(
            ["GET /health/live", "GET /health/ready"],
            workbenchOperations.Except(openApiOperations).Order(StringComparer.Ordinal));
        Assert.Equal(ApiInventory.OperationCount + 2, workbenchOperations.Count);
    }

    [Fact]
    public async Task V1Document_ContainsInventorySchemesAndCodeDerivedSecurity()
    {
        var document = await CreateClient("Staging").GetFromJsonAsync<JsonElement>(
            "/openapi/v1.json",
            TestContext.Current.CancellationToken);

        var paths = document.GetProperty("paths");
        Assert.Equal(
            ApiInventory.OperationCount,
            paths.EnumerateObject().Sum(path => CountOperations(path.Value)));

        var schemes = document
            .GetProperty("components")
            .GetProperty("securitySchemes");

        Assert.True(schemes.TryGetProperty("bearer", out _));
        Assert.True(schemes.TryGetProperty("cookie", out _));
        Assert.True(schemes.TryGetProperty("apiKey", out _));

        var login = paths
            .GetProperty("/api/v1/auth/login")
            .GetProperty("post");

        Assert.True(
            !login.TryGetProperty("security", out var loginSecurity)
            || loginSecurity.GetArrayLength() == 0);

        var auditQuery = paths
            .GetProperty("/api/v1/admin/audit-logs")
            .GetProperty("get");

        Assert.Equal(3, auditQuery.GetProperty("security").GetArrayLength());
    }

    private HttpClient CreateClient(string environment) =>
        _factory
            .WithWebHostBuilder(builder =>
            {
                builder.UseProductionLikeHost(environment, "openapi-contract-tests");
            })
            .CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    private static int CountOperations(JsonElement path) =>
        path.EnumerateObject().Count(property =>
            property.Name is "get" or "post" or "put" or "patch" or "delete" or "head" or "options");
}
