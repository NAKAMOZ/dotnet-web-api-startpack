using System.Net.Http.Headers;

namespace IntegrationTests.Infrastructure;

/// <summary>HTTP helper for the response-body/bearer transport.</summary>
public sealed class BearerClient(HttpClient client)
{
    public HttpClient Http { get; } = client;

    public void Authenticate(string accessToken) =>
        Http.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", accessToken);
}
