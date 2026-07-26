using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace IntegrationTests.Documentation;

/// <summary>
/// OpenAPI is authoritative for mechanical facts; endpoint Markdown is authoritative for
/// narrative. These tests enforce the overlap in both directions (§19).
/// </summary>
public class DocumentationSyncTests : IClassFixture<WebApplicationFactory<Program>>
{
    private static readonly string[] RequiredSections =
    [
        "Purpose",
        "HTTP method",
        "Route",
        "Authentication requirements",
        "Authorization requirements",
        "Request headers",
        "Route parameters",
        "Query parameters",
        "Request body",
        "Validation rules",
        "Success response",
        "Error responses",
        "Example request",
        "Example response",
        "Security considerations",
        "Related endpoints",
    ];

    private static readonly HashSet<string> HttpMethods =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "get",
            "post",
            "put",
            "patch",
            "delete",
            "head",
            "options",
        };

    private readonly WebApplicationFactory<Program> _factory;

    public DocumentationSyncTests(WebApplicationFactory<Program> factory) => _factory = factory;

    [Fact]
    public async Task EndpointDocuments_MatchTheGeneratedOperationSetAndMechanicalFacts()
    {
        var openApiOperations = await LoadOpenApiOperationsAsync();
        var markdownOperations = LoadMarkdownOperations();

        Assert.Equal(
            openApiOperations.Keys.Order(StringComparer.Ordinal),
            markdownOperations.Keys.Order(StringComparer.Ordinal));

        foreach (var (key, openApi) in openApiOperations)
        {
            var markdown = markdownOperations[key];

            Assert.Equal(openApi.Method, markdown.Method);
            Assert.Equal(openApi.Route, markdown.Route);
            Assert.Equal(openApi.Auth, markdown.Auth);
        }
    }

    [Fact]
    public void EndpointDocuments_ContainTheSixteenRequiredSectionsInOrder()
    {
        foreach (var document in LoadMarkdownOperations().Values)
        {
            var headings = File.ReadLines(document.FilePath)
                .Where(line => line.StartsWith("## ", StringComparison.Ordinal))
                .Select(line => line[3..])
                .ToArray();

            Assert.Equal(RequiredSections, headings);
        }
    }

    private async Task<Dictionary<string, EndpointContract>> LoadOpenApiOperationsAsync()
    {
        var client = _factory
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Staging");
                builder.UseTrustedTestProxy();
                builder.UseSetting(
                    "ConnectionStrings:Postgres",
                    "Host=localhost;Database=documentation-sync-tests");
            })
            .CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var document = await client.GetFromJsonAsync<JsonElement>(
            "/openapi/v1.json",
            TestContext.Current.CancellationToken);

        var operations = new Dictionary<string, EndpointContract>(StringComparer.Ordinal);

        foreach (var path in document.GetProperty("paths").EnumerateObject())
        {
            foreach (var operation in path.Value.EnumerateObject().Where(item => HttpMethods.Contains(item.Name)))
            {
                var method = operation.Name.ToUpperInvariant();
                var auth = operation.Value.TryGetProperty("security", out var security)
                           && security.GetArrayLength() > 0
                    ? "required"
                    : "anonymous";

                operations.Add(
                    Key(method, path.Name),
                    new EndpointContract(method, path.Name, auth, FilePath: string.Empty));
            }
        }

        return operations;
    }

    private static Dictionary<string, EndpointContract> LoadMarkdownOperations()
    {
        var documentationRoot = Path.Combine(FindRepositoryRoot(), "Documentation");
        var operations = new Dictionary<string, EndpointContract>(StringComparer.Ordinal);

        foreach (var file in Directory.EnumerateFiles(
                     documentationRoot,
                     "*.md",
                     SearchOption.AllDirectories))
        {
            if (Path.GetFileName(file).StartsWith('_'))
            {
                continue;
            }

            var lines = File.ReadAllLines(file);

            if (lines.Length == 0 || !string.Equals(lines[0], "---", StringComparison.Ordinal))
            {
                continue;
            }

            var closingDelimiter = Array.IndexOf(lines, "---", startIndex: 1);
            Assert.True(closingDelimiter > 1, $"Front matter is not closed in {file}.");

            var frontMatter = lines[1..closingDelimiter]
                .Select(line => line.Split(':', count: 2))
                .Where(parts => parts.Length == 2)
                .ToDictionary(
                    parts => parts[0].Trim(),
                    parts => parts[1].Trim(),
                    StringComparer.Ordinal);

            Assert.True(frontMatter.TryGetValue("method", out var method), $"Missing method in {file}.");
            Assert.True(frontMatter.TryGetValue("route", out var route), $"Missing route in {file}.");
            Assert.True(frontMatter.TryGetValue("auth", out var auth), $"Missing auth in {file}.");
            Assert.True(
                auth is "anonymous" or "required",
                $"Auth must be anonymous or required in {file}.");

            var normalizedMethod = method.ToUpperInvariant();
            operations.Add(
                Key(normalizedMethod, route),
                new EndpointContract(normalizedMethod, route, auth, file));
        }

        return operations;
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "dotnet-web-api-startpack.csproj")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the repository root.");
    }

    private static string Key(string method, string route) => method + " " + route;

    private sealed record EndpointContract(
        string Method,
        string Route,
        string Auth,
        string FilePath);
}
