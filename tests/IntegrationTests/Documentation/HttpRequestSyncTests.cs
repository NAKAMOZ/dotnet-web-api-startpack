using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace IntegrationTests.Documentation;

public sealed partial class HttpRequestSyncTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public HttpRequestSyncTests(WebApplicationFactory<Program> factory) => _factory = factory;

    [Fact]
    public async Task HttpFiles_CoverEveryOpenApiOperationWithoutUnknownRequests()
    {
        var client = _factory
            .WithWebHostBuilder(builder =>
            {
                builder.UseProductionLikeHost("Staging", "http-request-sync-tests");
            })
            .CreateClient();

        using var openApi = await JsonDocument.ParseAsync(
            await client.GetStreamAsync(
                "/openapi/v1.json",
                TestContext.Current.CancellationToken),
            cancellationToken: TestContext.Current.CancellationToken);

        var documented = OpenApiOperations(openApi.RootElement);
        var requests = HttpFileOperations(RepositoryPaths.Root);

        Assert.True(
            documented.SetEquals(requests),
            $"Missing HTTP requests: [{string.Join(", ", documented.Except(requests))}]. " +
            $"Unknown HTTP requests: [{string.Join(", ", requests.Except(documented))}].");
        Assert.Equal(ApiInventory.OperationCount, requests.Count);
    }

    private static SortedSet<string> OpenApiOperations(JsonElement document)
    {
        var operations = new SortedSet<string>(StringComparer.Ordinal);

        foreach (var path in document.GetProperty("paths").EnumerateObject())
        {
            foreach (var operation in path.Value.EnumerateObject())
            {
                if (operation.Name is "get" or "post" or "put" or "patch" or "delete")
                {
                    operations.Add($"{operation.Name.ToUpperInvariant()} {path.Name}");
                }
            }
        }

        return operations;
    }

    private static SortedSet<string> HttpFileOperations(string repositoryRoot)
    {
        var operations = new SortedSet<string>(StringComparer.Ordinal);

        foreach (var file in Directory.EnumerateFiles(
                     Path.Combine(repositoryRoot, "http"),
                     "*.http"))
        {
            foreach (Match match in RequestLine().Matches(File.ReadAllText(file)))
            {
                var path = match.Groups["url"].Value
                    .Replace("{{api}}", "/api/v1", StringComparison.Ordinal)
                    .Replace("{{host}}", string.Empty, StringComparison.Ordinal);
                path = path.Split('?', 2)[0];
                path = Variable().Replace(path, "{$1}");

                operations.Add($"{match.Groups["method"].Value} {path}");
            }
        }

        return operations;
    }

    [GeneratedRegex(
        @"^(?<method>GET|POST|PUT|PATCH|DELETE)\s+(?<url>\S+)\s*$",
        RegexOptions.Multiline | RegexOptions.CultureInvariant)]
    private static partial Regex RequestLine();

    [GeneratedRegex(@"\{\{([^}]+)\}\}", RegexOptions.CultureInvariant)]
    private static partial Regex Variable();
}
