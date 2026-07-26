using System.Net.Http.Json;
using System.Text.Json;
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
    public async Task V1Document_ContainsInventorySchemesAndCodeDerivedSecurity()
    {
        var document = await CreateClient("Staging").GetFromJsonAsync<JsonElement>(
            "/openapi/v1.json",
            TestContext.Current.CancellationToken);

        var paths = document.GetProperty("paths");
        Assert.Equal(43, paths.EnumerateObject().Sum(path => CountOperations(path.Value)));

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
                builder.UseEnvironment(environment);
                builder.UseTrustedTestProxy();
                builder.UseSetting(
                    "ConnectionStrings:Postgres",
                    "Host=localhost;Database=openapi-contract-tests");
            })
            .CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    private static int CountOperations(JsonElement path) =>
        path.EnumerateObject().Count(property =>
            property.Name is "get" or "post" or "put" or "patch" or "delete" or "head" or "options");
}
