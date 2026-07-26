namespace IntegrationTests.Infrastructure;

/// <summary>
/// HTTP helper for cookie transport. The complete login/CSRF handshake is enabled when the
/// §12 auth actions replace their current 501 responses.
/// </summary>
public sealed class CookieClient(HttpClient client)
{
    public HttpClient Http { get; } = client;

    public HttpRequestMessage CreateRequest(HttpMethod method, string path, string cookie, string? csrf = null)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.Add("Cookie", cookie);

        if (csrf is not null)
        {
            request.Headers.Add("X-CSRF-Token", csrf);
        }

        return request;
    }
}
